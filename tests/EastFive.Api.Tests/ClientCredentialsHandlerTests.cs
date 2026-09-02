using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

using Microsoft.Extensions.Configuration;

using EastFive.Api.Auth;

namespace EastFive.Api.Tests;

/// <summary>
/// <see cref="ClientCredentialsHandler"/> — the machine-client side of the
/// RFC 6749 s4.4 grant. Everything below the handler is a scripted
/// <see cref="HttpMessageHandler"/>: the token endpoint and the downstream
/// resource are both answered from the script, so the tests pin down exactly
/// what the handler sends and how often.
/// </summary>
public class ClientCredentialsHandlerTests
{
    private static readonly Uri tokenEndpoint = new("https://auth.example.test/oauth/token");
    private static readonly Uri resource = new("https://api.example.test/api/thing");
    private const string clientId = "engine-cli";
    private const string clientSecret = "s3cr=t:with/odd chars";

    [Fact]
    public async Task FirstCall_FetchesTokenWithBasicAuth_ThenBearerOnDownstream()
    {
        var script = new Script();
        script.TokenResponses.Enqueue(TokenJson("tok-1", expiresIn: 3600));
        using var client = Build(script, scope: "rosemary.engine");

        var response = await client.GetAsync(resource);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokenRequest = Assert.Single(script.TokenRequests);
        Assert.Equal(HttpMethod.Post, tokenRequest.Method);
        Assert.Equal("Basic", tokenRequest.AuthScheme);
        var expectedBasic = Convert.ToBase64String(Encoding.UTF8.GetBytes(
            $"{Uri.EscapeDataString(clientId)}:{Uri.EscapeDataString(clientSecret)}"));
        Assert.Equal(expectedBasic, tokenRequest.AuthParameter);
        Assert.Equal("application/x-www-form-urlencoded", tokenRequest.ContentType);
        Assert.Equal("client_credentials", tokenRequest.Form["grant_type"]);
        Assert.Equal("rosemary.engine", tokenRequest.Form["scope"]);
        Assert.False(tokenRequest.Form.ContainsKey("client_secret"), "secret must travel in the Basic header only");

        var downstream = Assert.Single(script.ResourceRequests);
        Assert.Equal("Bearer", downstream.AuthScheme);
        Assert.Equal("tok-1", downstream.AuthParameter);
    }

    [Fact]
    public async Task NoScope_OmitsScopeParameter()
    {
        var script = new Script();
        script.TokenResponses.Enqueue(TokenJson("tok-1", expiresIn: 3600));
        using var client = Build(script, scope: null);

        await client.GetAsync(resource);

        var tokenRequest = Assert.Single(script.TokenRequests);
        Assert.False(tokenRequest.Form.ContainsKey("scope"));
    }

    [Fact]
    public async Task SecondCall_ReusesCachedToken()
    {
        var script = new Script();
        script.TokenResponses.Enqueue(TokenJson("tok-1", expiresIn: 3600));
        using var client = Build(script);

        await client.GetAsync(resource);
        await client.GetAsync(resource);

        Assert.Single(script.TokenRequests);
        Assert.Equal(2, script.ResourceRequests.Count);
        Assert.All(script.ResourceRequests, r => Assert.Equal("tok-1", r.AuthParameter));
    }

    [Fact]
    public async Task NearExpiry_RefetchesToken()
    {
        var script = new Script();
        var clock = new FakeClock(DateTimeOffset.Parse("2026-09-01T12:00:00Z"));
        script.TokenResponses.Enqueue(TokenJson("tok-1", expiresIn: 90));
        script.TokenResponses.Enqueue(TokenJson("tok-2", expiresIn: 90));
        using var client = Build(script, clock: clock);

        await client.GetAsync(resource);
        clock.Advance(TimeSpan.FromSeconds(29)); // 61s of life left, outside the 60s guard band: reuse
        await client.GetAsync(resource);
        Assert.Single(script.TokenRequests);

        clock.Advance(TimeSpan.FromSeconds(2)); // 59s left, inside exp - 60s: refetch
        await client.GetAsync(resource);

        Assert.Equal(2, script.TokenRequests.Count);
        Assert.Equal(new[] { "tok-1", "tok-1", "tok-2" }, script.ResourceRequests.Select(r => r.AuthParameter));
    }

    [Fact]
    public async Task Downstream401_RefreshesOnceAndRetriesOnce()
    {
        var script = new Script();
        script.TokenResponses.Enqueue(TokenJson("tok-1", expiresIn: 3600));
        script.TokenResponses.Enqueue(TokenJson("tok-2", expiresIn: 3600));
        script.ResourceResponses.Enqueue(() => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        script.ResourceResponses.Enqueue(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("after refresh"),
        });
        using var client = Build(script);

        var response = await client.PostAsync(resource, new StringContent("payload"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("after refresh", await response.Content.ReadAsStringAsync());
        Assert.Equal(2, script.TokenRequests.Count);
        Assert.Equal(new[] { "tok-1", "tok-2" }, script.ResourceRequests.Select(r => r.AuthParameter));
        // the retried request carried the same body
        Assert.All(script.ResourceRequests, r => Assert.Equal("payload", r.Body));
    }

    [Fact]
    public async Task Persistent401_IsReturnedAfterOneRetry()
    {
        var script = new Script();
        script.TokenResponses.Enqueue(TokenJson("tok-1", expiresIn: 3600));
        script.TokenResponses.Enqueue(TokenJson("tok-2", expiresIn: 3600));
        script.ResourceResponses.Enqueue(() => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        script.ResourceResponses.Enqueue(() => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var client = Build(script);

        var response = await client.GetAsync(resource);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(2, script.TokenRequests.Count);
        Assert.Equal(2, script.ResourceRequests.Count);
    }

    [Fact]
    public async Task TokenEndpointError_SurfacesRfc6749ErrorAndDescription()
    {
        var script = new Script();
        script.TokenResponses.Enqueue(() => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                """{"error":"invalid_client","error_description":"Client authentication failed."}""",
                Encoding.UTF8, "application/json"),
        });
        using var client = Build(script);

        var ex = await Assert.ThrowsAsync<ClientCredentialsTokenException>(() => client.GetAsync(resource));

        Assert.Equal("invalid_client", ex.Error);
        Assert.Equal("Client authentication failed.", ex.ErrorDescription);
        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Contains("invalid_client", ex.Message);
        Assert.Contains("Client authentication failed.", ex.Message);
        Assert.Empty(script.ResourceRequests);
    }

    [Fact]
    public async Task TokenEndpointNonJsonFailure_StillSurfacesStatus()
    {
        var script = new Script();
        script.TokenResponses.Enqueue(() => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("<html>bad gateway</html>", Encoding.UTF8, "text/html"),
        });
        using var client = Build(script);

        var ex = await Assert.ThrowsAsync<ClientCredentialsTokenException>(() => client.GetAsync(resource));

        Assert.Equal(HttpStatusCode.BadGateway, ex.StatusCode);
        Assert.Null(ex.Error);
        Assert.Contains("502", ex.Message);
    }

    [Fact]
    public async Task ConcurrentFirstCalls_CoalesceIntoOneTokenFetch()
    {
        var script = new Script();
        var release = new TaskCompletionSource();
        script.TokenResponsesAsync.Enqueue(async () =>
        {
            await release.Task;
            return TokenJson("tok-1", expiresIn: 3600)();
        });
        using var client = Build(script);

        var calls = Enumerable.Range(0, 8).Select(_ => client.GetAsync(resource)).ToArray();
        // let every caller reach the token fetch before it completes
        await Task.Delay(50);
        release.SetResult();
        await Task.WhenAll(calls);

        Assert.Single(script.TokenRequests);
        Assert.Equal(8, script.ResourceRequests.Count);
        Assert.All(script.ResourceRequests, r => Assert.Equal("tok-1", r.AuthParameter));
    }

    [Fact]
    public void FromConfiguration_ReadsEndpointClientIdSecretAndOptionalScope()
    {
        // ConfigurationString reads process-global configuration; swap it in and restore the
        // harness's own sources (appsettings.json + environment) afterwards.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Test.Rosemary.TokenEndpoint"] = tokenEndpoint.ToString(),
                ["Test.Rosemary.ClientId"] = clientId,
                ["Test.Rosemary.ClientSecret"] = clientSecret,
            })
            .Build();
        EastFive.Web.Configuration.ConfigurationExtensions.Initialize(configuration);
        try
        {
            using var handler = ClientCredentialsHandler.FromConfiguration(
                "Test.Rosemary.TokenEndpoint", "Test.Rosemary.ClientId", "Test.Rosemary.ClientSecret",
                scopeKey: "Test.Rosemary.Scope");

            Assert.Equal(tokenEndpoint, handler.TokenEndpoint);
            Assert.Equal(clientId, handler.ClientId);
            Assert.Null(handler.Scope);

            Assert.Throws<EastFive.Web.ConfigurationException>(() =>
                ClientCredentialsHandler.FromConfiguration(
                    "Test.Rosemary.TokenEndpoint", "Test.Rosemary.ClientId", "Test.Rosemary.MissingSecret"));
        }
        finally
        {
            var restored = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables()
                .Build();
            EastFive.Web.Configuration.ConfigurationExtensions.Initialize(restored);
        }
    }

    #region Scripting

    private static HttpClient Build(Script script, string? scope = "rosemary.engine", FakeClock? clock = null)
    {
        var handler = new ClientCredentialsHandler(tokenEndpoint, clientId, clientSecret, scope,
            timeProvider: clock)
        {
            InnerHandler = script,
        };
        return new HttpClient(handler);
    }

    private static Func<HttpResponseMessage> TokenJson(string accessToken, int expiresIn) =>
        () => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""{"access_token":"{{accessToken}}","token_type":"Bearer","expires_in":{{expiresIn}},"scope":"rosemary.engine"}""",
                Encoding.UTF8, "application/json"),
        };

    private sealed record Observed(HttpMethod Method, string? AuthScheme, string? AuthParameter,
        string? ContentType, string? Body, IReadOnlyDictionary<string, string> Form);

    private sealed class Script : HttpMessageHandler
    {
        public ConcurrentQueue<Func<HttpResponseMessage>> TokenResponses { get; } = new();
        public ConcurrentQueue<Func<Task<HttpResponseMessage>>> TokenResponsesAsync { get; } = new();
        public ConcurrentQueue<Func<HttpResponseMessage>> ResourceResponses { get; } = new();
        public List<Observed> TokenRequests { get; } = new();
        public List<Observed> ResourceRequests { get; } = new();

        private readonly object gate = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var observed = await ObserveAsync(request);
            if (request.RequestUri == tokenEndpoint)
            {
                lock (gate) TokenRequests.Add(observed);
                if (TokenResponsesAsync.TryDequeue(out var pending))
                    return await pending();
                if (TokenResponses.TryDequeue(out var scripted))
                    return scripted();
                throw new InvalidOperationException("unexpected token request");
            }

            lock (gate) ResourceRequests.Add(observed);
            if (ResourceResponses.TryDequeue(out var resourceResponse))
                return resourceResponse();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
        }

        private static async Task<Observed> ObserveAsync(HttpRequestMessage request)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
            var form = new Dictionary<string, string>();
            if (request.Content?.Headers.ContentType?.MediaType == "application/x-www-form-urlencoded" && body is not null)
                foreach (var pair in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    var kv = pair.Split('=', 2);
                    form[Uri.UnescapeDataString(kv[0])] = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : string.Empty;
                }
            return new Observed(request.Method,
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                request.Content?.Headers.ContentType?.MediaType,
                body, form);
        }
    }

    private sealed class FakeClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset now = now;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan by) => now += by;
    }

    #endregion
}
