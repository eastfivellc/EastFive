using System.Net.Http;
using System.Security.Claims;
using System.Text;

using Microsoft.AspNetCore.Http;

using EastFive.Api;
using EastFive.Api.Core;
using EastFive.Azure.OAuth;
using EastFive.Azure.OAuth.Server;
using EastFive.Azure.Persistence.AzureStorageTables;
using EastFive.Web.Configuration;

namespace EastFive.Azure.Tests.OAuth;

/// <summary>
/// Request builders for driving the OAuth endpoints through the real dispatch
/// pipeline (<c>TestSession.DispatchRawAsync</c>).
/// </summary>
internal static class OAuthRequests
{
    public const string TokenPath = "/oauth/token";

    /// <summary>
    /// <c>application/x-www-form-urlencoded</c> POST over a genuine ASP.NET Core
    /// request (<see cref="DefaultHttpContext"/>) so <c>IHttpRequest.Form</c> is the
    /// production form reader — the framework's test <see cref="HttpRequest"/> does
    /// not implement <c>Form</c>.
    /// </summary>
    public static IHttpRequest FormPost(string path,
        IReadOnlyDictionary<string, string> form,
        (string clientId, string clientSecret)? basic = null,
        string? bearer = null)
    {
        var context = new DefaultHttpContext();
        var request = context.Request;
        request.Method = HttpMethods.Post;
        request.Scheme = "https";
        request.Host = new HostString("localhost");
        request.Path = path;

        var body = string.Join("&", form.Select(kvp =>
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
        var bytes = Encoding.UTF8.GetBytes(body);
        request.Body = new MemoryStream(bytes);
        request.ContentType = "application/x-www-form-urlencoded";
        request.ContentLength = bytes.Length;

        if (basic is var (clientId, clientSecret))
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                $"{Uri.EscapeDataString(clientId)}:{Uri.EscapeDataString(clientSecret)}"));
            request.Headers.Authorization = $"Basic {credentials}";
        }
        if (bearer is not null)
            request.Headers.Authorization = $"Bearer {bearer}";

        return new CoreHttpRequest(request, razorViewEngine: null!, CancellationToken.None);
    }

    /// <summary>Body-less request (GET / action POST) with an optional bearer token.</summary>
    public static IHttpRequest Bare(HttpMethod method, string path,
        string? bearer = null,
        IReadOnlyDictionary<string, string>? query = null)
    {
        var queryString = query is null || query.Count == 0
            ? string.Empty
            : "?" + string.Join("&", query.Select(kvp =>
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
        var request = new EastFive.Api.HttpRequest(new Uri($"https://localhost{path}{queryString}"))
        {
            Method = method,
        };
        if (bearer is not null)
            request.Headers["Authorization"] = new[] { $"Bearer {bearer}" };
        return request;
    }
}

/// <summary>Storage seeding + token minting for the client-credentials tests.</summary>
public static class OAuthFixtures
{
    public const string RegisteredScope = "rosemary.engine rosemary.read";

    public sealed record SeededClient(ClientCredential Client, string Secret);

    /// <summary>
    /// Registers a confidential client the way <c>POST /OAuth/ClientCredential</c> would
    /// store it (hashed secret, active, client_credentials grant) and returns the plaintext.
    /// </summary>
    public static async Task<SeededClient> SeedConfidentialClientAsync(
        string grantTypes = ClientCredential.GrantTypeValues.ClientCredentials,
        string scope = RegisteredScope,
        bool isActive = true,
        string tokenEndpointAuthMethod = ClientCredential.TokenEndpointAuthMethods.ClientSecretBasic)
    {
        TestConfiguration.Ensure();
        var secret = OAuthServer.GenerateSecret();
        var now = DateTime.UtcNow;
        var client = new ClientCredential
        {
            @ref = Guid.NewGuid().AsRef<ClientCredential>(),
            clientId = "cc-" + Guid.NewGuid().ToString("N"),
            clientType = ClientCredential.ClientTypes.Confidential,
            clientSecret = OAuthServer.ComputeSecretHash(secret),
            grantTypes = grantTypes,
            tokenEndpointAuthMethod = tokenEndpointAuthMethod,
            scope = scope,
            name = "client-credentials test client",
            isActive = isActive,
            createdAt = now,
            updatedAt = now,
        };
        var stored = await client.StorageCreateAsync(
            created => true,
            onAlreadyExists: () => false);
        if (!stored)
            throw new InvalidOperationException($"Could not seed client {client.clientId}.");
        return new SeededClient(client, secret);
    }

    /// <summary>A first-party session-style token carrying the superadmin role claim.</summary>
    public static string SuperAdminToken()
    {
        TestConfiguration.Ensure();
        var scope = EastFive.Security.AppSettings.TokenScope.ConfigurationUri();
        return EastFive.Api.Auth.JwtTools.CreateToken(
                Guid.NewGuid(), scope, TimeSpan.FromMinutes(5),
                new Dictionary<string, string>
                {
                    [ClaimTypes.Role] = EastFive.Api.Auth.ClaimValues.Roles.SuperAdmin,
                },
            (token, issued) => token,
            missing => throw new InvalidOperationException($"Missing config {missing}"),
            (key, why) => throw new InvalidOperationException($"Invalid config {key}: {why}"));
    }
}

/// <summary>
/// Resource endpoints gated on OAuth scopes, dispatched through the same pipeline
/// the token endpoint runs in — proves a client-credentials token is usable (and
/// only usable) where its <c>scp</c> claim says.
/// </summary>
[FunctionViewController(Route = "scope-probe")]
public static class ScopeProbe
{
    public const string GrantedScope = "rosemary.engine";
    public const string UngrantedScope = "rosemary.admin";

    [HttpGet]
    [RequiredScope(GrantedScope)]
    public static IHttpResponse RequiresGrantedScope(NoContentResponse onAllowed) => onAllowed();

    [HttpGet]
    [RequiredScope(UngrantedScope)]
    public static IHttpResponse RequiresUngrantedScope(NoContentResponse onAllowed) => onAllowed();
}
