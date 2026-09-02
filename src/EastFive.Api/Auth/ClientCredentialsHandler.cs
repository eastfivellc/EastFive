using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using EastFive.Web.Configuration;

namespace EastFive.Api.Auth
{
    /// <summary>
    /// Client side of the OAuth 2.0 client-credentials grant (RFC 6749 s4.4) for a
    /// first-party machine client. Compose it into an <see cref="HttpClient"/> and every
    /// request goes out with <c>Authorization: Bearer</c>:
    /// <code>
    /// var handler = ClientCredentialsHandler.FromConfiguration(
    ///     "Rosemary.TokenEndpoint", "Rosemary.ClientId", "Rosemary.ClientSecret", "Rosemary.Scope");
    /// var http = new HttpClient(handler) { BaseAddress = rosemaryBaseUri };
    /// </code>
    /// Behavior:
    /// <list type="bullet">
    /// <item>Lazily POSTs <c>grant_type=client_credentials</c> (+ optional <c>scope</c>) to the
    /// token endpoint, authenticating with HTTP Basic (RFC 6749 s2.3.1: id and secret
    /// form-urlencoded, then base64). The secret never travels in the form body.</item>
    /// <item>Caches the access token until <c>exp - 60s</c> (from <c>expires_in</c>); concurrent
    /// first-fetches coalesce into one token request.</item>
    /// <item>On a 401 from the downstream request, refreshes the token once and retries the
    /// request once (body is buffered so it can be re-sent). A second 401 is returned as-is.</item>
    /// <item>Token-endpoint failures throw <see cref="ClientCredentialsTokenException"/> carrying the
    /// RFC 6749 s5.2 <c>error</c> / <c>error_description</c> when the body has them.</item>
    /// </list>
    /// If no <see cref="DelegatingHandler.InnerHandler"/> has been assigned by the first send,
    /// an <see cref="HttpClientHandler"/> is created, so <c>new HttpClient(handler)</c> just works;
    /// <c>IHttpClientFactory</c> pipelines assign their own inner handler as usual.
    /// </summary>
    public class ClientCredentialsHandler : DelegatingHandler
    {
        public Uri TokenEndpoint { get; }

        public string ClientId { get; }

        /// <summary>Requested scope (space delimited) or null to receive the client's registered scope.</summary>
        public string Scope { get; }

        /// <summary>How long before <c>exp</c> the cached token is considered stale. Default 60s.</summary>
        public TimeSpan RefreshGuardBand { get; init; } = TimeSpan.FromSeconds(60);

        private readonly string clientSecret;
        private readonly TimeProvider clock;
        private readonly SemaphoreSlim tokenGate = new SemaphoreSlim(1, 1);
        private readonly object innerHandlerGate = new object();
        private CachedToken cached;

        private sealed class CachedToken
        {
            public string AccessToken;
            public DateTimeOffset RefreshAfter;
        }

        public ClientCredentialsHandler(Uri tokenEndpoint, string clientId, string clientSecret,
            string scope = null, TimeProvider timeProvider = null)
        {
            if (tokenEndpoint == null)
                throw new ArgumentNullException(nameof(tokenEndpoint));
            if (string.IsNullOrWhiteSpace(clientId))
                throw new ArgumentException("client_id is required.", nameof(clientId));
            if (string.IsNullOrWhiteSpace(clientSecret))
                throw new ArgumentException("client_secret is required.", nameof(clientSecret));

            this.TokenEndpoint = tokenEndpoint;
            this.ClientId = clientId;
            this.clientSecret = clientSecret;
            this.Scope = string.IsNullOrWhiteSpace(scope) ? null : scope;
            this.clock = timeProvider ?? TimeProvider.System;
        }

        /// <summary>
        /// Builds a handler from process configuration so a consumer keeps the client id in
        /// appsettings and the secret in Key Vault: <paramref name="endpointKey"/>,
        /// <paramref name="clientIdKey"/>, <paramref name="clientSecretKey"/> are REQUIRED keys
        /// (a missing one throws <see cref="EastFive.Web.ConfigurationException"/>);
        /// <paramref name="scopeKey"/> is optional and its absence means "registered scope".
        /// </summary>
        public static ClientCredentialsHandler FromConfiguration(string endpointKey,
            string clientIdKey, string clientSecretKey, string scopeKey = null)
        {
            return endpointKey.ConfigurationUri(
                endpoint => clientIdKey.ConfigurationString(
                    clientId => clientSecretKey.ConfigurationString(
                        clientSecret =>
                        {
                            var scope = string.IsNullOrWhiteSpace(scopeKey)
                                ? null
                                : scopeKey.ConfigurationString(s => s, why => (string)null);
                            return new ClientCredentialsHandler(endpoint, clientId, clientSecret, scope);
                        })));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            EnsureInnerHandler();

            // Buffer up front so a 401 retry can re-send the same body.
            if (request.Content != null)
                await request.Content.LoadIntoBufferAsync();

            var accessToken = await GetAccessTokenAsync(staleToken: null, cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await base.SendAsync(request, cancellationToken);
            if (response.StatusCode != HttpStatusCode.Unauthorized)
                return response;

            response.Dispose();
            var refreshedToken = await GetAccessTokenAsync(staleToken: accessToken, cancellationToken);
            var retry = await CloneAsync(request);
            retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshedToken);
            return await base.SendAsync(retry, cancellationToken);
        }

        /// <summary>
        /// Returns a usable access token, fetching when there is none, when the cached one is
        /// inside the guard band, or when <paramref name="staleToken"/> (a token that just got a
        /// 401) is still the cached one. Callers racing for the first token share one fetch.
        /// </summary>
        private async Task<string> GetAccessTokenAsync(string staleToken, CancellationToken cancellationToken)
        {
            var current = Volatile.Read(ref cached);
            if (IsUsable(current, staleToken))
                return current.AccessToken;

            await tokenGate.WaitAsync(cancellationToken);
            try
            {
                current = cached;
                if (IsUsable(current, staleToken))
                    return current.AccessToken;

                var fresh = await FetchTokenAsync(cancellationToken);
                Volatile.Write(ref cached, fresh);
                return fresh.AccessToken;
            }
            finally
            {
                tokenGate.Release();
            }
        }

        private bool IsUsable(CachedToken token, string staleToken)
        {
            if (token == null)
                return false;
            if (staleToken != null && string.Equals(token.AccessToken, staleToken, StringComparison.Ordinal))
                return false;
            return clock.GetUtcNow() < token.RefreshAfter;
        }

        private async Task<CachedToken> FetchTokenAsync(CancellationToken cancellationToken)
        {
            var form = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
            };
            if (this.Scope != null)
                form.Add(new KeyValuePair<string, string>("scope", this.Scope));

            using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, this.TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(form),
            };
            // RFC 6749 s2.3.1: form-urlencode id and secret, then HTTP Basic.
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                $"{Uri.EscapeDataString(this.ClientId)}:{Uri.EscapeDataString(this.clientSecret)}"));
            tokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
            tokenRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var tokenResponse = await base.SendAsync(tokenRequest, cancellationToken);
            var body = tokenResponse.Content == null
                ? string.Empty
                : await tokenResponse.Content.ReadAsStringAsync(cancellationToken);
            var requestedAt = clock.GetUtcNow();

            if (!tokenResponse.IsSuccessStatusCode)
                throw ClientCredentialsTokenException.FromResponse(this.TokenEndpoint, tokenResponse.StatusCode, body);

            string accessToken = null;
            long? expiresIn = null;
            try
            {
                using var json = JsonDocument.Parse(body);
                var root = json.RootElement;
                if (root.TryGetProperty("access_token", out var accessTokenElement)
                        && accessTokenElement.ValueKind == JsonValueKind.String)
                    accessToken = accessTokenElement.GetString();
                if (root.TryGetProperty("expires_in", out var expiresInElement))
                {
                    if (expiresInElement.ValueKind == JsonValueKind.Number && expiresInElement.TryGetInt64(out var seconds))
                        expiresIn = seconds;
                    else if (expiresInElement.ValueKind == JsonValueKind.String
                            && long.TryParse(expiresInElement.GetString(), out var parsedSeconds))
                        expiresIn = parsedSeconds;
                }
            }
            catch (JsonException)
            {
                // fall through: reported as a malformed success below
            }

            if (string.IsNullOrWhiteSpace(accessToken))
                throw new ClientCredentialsTokenException(this.TokenEndpoint, tokenResponse.StatusCode,
                    error: null, errorDescription: null, responseBody: body,
                    message: $"Token endpoint {this.TokenEndpoint} answered {(int)tokenResponse.StatusCode} without an access_token.");

            // No expires_in (RECOMMENDED, not required): do not cache.
            var refreshAfter = expiresIn.HasValue
                ? requestedAt + TimeSpan.FromSeconds(expiresIn.Value) - this.RefreshGuardBand
                : requestedAt;
            return new CachedToken { AccessToken = accessToken, RefreshAfter = refreshAfter };
        }

        private void EnsureInnerHandler()
        {
            if (this.InnerHandler != null)
                return;
            lock (innerHandlerGate)
            {
                if (this.InnerHandler == null)
                    this.InnerHandler = new HttpClientHandler();
            }
        }

        private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Version = request.Version,
                VersionPolicy = request.VersionPolicy,
            };
            foreach (var header in request.Headers)
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            foreach (var option in request.Options)
                clone.Options.TryAdd(option.Key, option.Value);

            if (request.Content != null)
            {
                // content was buffered before the first send, so this re-reads the buffer
                var bytes = await request.Content.ReadAsByteArrayAsync();
                var content = new ByteArrayContent(bytes);
                foreach (var header in request.Content.Headers)
                    content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                clone.Content = content;
            }
            return clone;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                tokenGate.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// The token endpoint refused or malformed a client-credentials request.
    /// <see cref="Error"/> / <see cref="ErrorDescription"/> are the RFC 6749 s5.2 fields when
    /// the response body carried them (null otherwise); <see cref="ResponseBody"/> is the raw
    /// body for diagnostics. The HTTP status is on <see cref="HttpRequestException.StatusCode"/>.
    /// </summary>
    public class ClientCredentialsTokenException : HttpRequestException
    {
        public Uri TokenEndpoint { get; }

        public string Error { get; }

        public string ErrorDescription { get; }

        public string ResponseBody { get; }

        public ClientCredentialsTokenException(Uri tokenEndpoint, HttpStatusCode statusCode,
            string error, string errorDescription, string responseBody, string message)
            : base(message, inner: null, statusCode: statusCode)
        {
            this.TokenEndpoint = tokenEndpoint;
            this.Error = error;
            this.ErrorDescription = errorDescription;
            this.ResponseBody = responseBody;
        }

        public static ClientCredentialsTokenException FromResponse(Uri tokenEndpoint,
            HttpStatusCode statusCode, string body)
        {
            string error = null;
            string description = null;
            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    using var json = JsonDocument.Parse(body);
                    if (json.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        if (json.RootElement.TryGetProperty("error", out var errorElement)
                                && errorElement.ValueKind == JsonValueKind.String)
                            error = errorElement.GetString();
                        if (json.RootElement.TryGetProperty("error_description", out var descriptionElement)
                                && descriptionElement.ValueKind == JsonValueKind.String)
                            description = descriptionElement.GetString();
                    }
                }
                catch (JsonException)
                {
                    // non-JSON failure body (proxy/gateway page); reported by status + snippet
                }
            }

            var detail = error != null
                ? (description != null ? $"{error}: {description}" : error)
                : Snippet(body);
            var message = $"Token endpoint {tokenEndpoint} answered {(int)statusCode} {statusCode}"
                + (string.IsNullOrEmpty(detail) ? "." : $": {detail}");
            return new ClientCredentialsTokenException(tokenEndpoint, statusCode, error, description, body, message);
        }

        private static string Snippet(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return string.Empty;
            var trimmed = body.Trim();
            return trimmed.Length <= 200 ? trimmed : trimmed.Substring(0, 200) + "...";
        }
    }
}
