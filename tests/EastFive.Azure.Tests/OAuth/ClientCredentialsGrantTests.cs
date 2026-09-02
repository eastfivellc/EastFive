using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;

using Xunit;

using Newtonsoft.Json;

using EastFive.Api;
using EastFive.Api.Tests.Harness;
using EastFive.Azure.OAuth;
using EastFive.Azure.OAuth.Server;
using EastFive.Azure.Persistence.AzureStorageTables;

namespace EastFive.Azure.Tests.OAuth;

/// <summary>
/// RFC 6749 §4.4 client-credentials grant, end to end through the production
/// dispatch pipeline: <c>POST /oauth/token</c> issues a JWT for an admin-provisioned
/// confidential client, and that JWT is honored by <c>[RequiredScope]</c> gates
/// (and by nothing else). Storage is Azurite (client registrations are real rows).
/// </summary>
public class ClientCredentialsGrantTests : TestSession
{
    public ClientCredentialsGrantTests()
    {
        TestConfiguration.Ensure();
    }

    #region Token issuance

    [Fact]
    public async Task TokenIssued_WithHttpBasicClientAuthentication()
    {
        var seeded = await OAuthFixtures.SeedConfidentialClientAsync();

        var outcome = await RequestTokenAsync(
            form: new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["scope"] = ScopeProbe.GrantedScope,
            },
            basic: (seeded.Client.clientId, seeded.Secret));

        var token = Assert.IsType<TokenSuccessResponse>(outcome.Issued);
        Assert.Equal("Bearer", token.TokenType);
        Assert.Equal(ScopeProbe.GrantedScope, token.Scope);
        Assert.Null(token.RefreshToken);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.AccessToken);
        Assert.Equal(seeded.Client.clientId,
            jwt.Claims.Single(c => c.Type == OAuthServer.ClientIdClaimType).Value);
        Assert.Equal(ScopeProbe.GrantedScope,
            jwt.Claims.Single(c => c.Type == OAuthServer.ScopeClaimType).Value);
    }

    [Fact]
    public async Task TokenIssued_WithFormBodyClientAuthentication()
    {
        var seeded = await OAuthFixtures.SeedConfidentialClientAsync(
            tokenEndpointAuthMethod: ClientCredential.TokenEndpointAuthMethods.ClientSecretPost);

        var outcome = await RequestTokenAsync(
            form: new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = seeded.Client.clientId,
                ["client_secret"] = seeded.Secret,
            });

        var token = Assert.IsType<TokenSuccessResponse>(outcome.Issued);
        // no scope requested → the registered scope is granted
        Assert.Equal(OAuthFixtures.RegisteredScope, token.Scope);
    }

    [Fact]
    public async Task ExpiresIn_HonorsAccessTokenExpirationConfiguration()
    {
        var seeded = await OAuthFixtures.SeedConfidentialClientAsync();

        var outcome = await RequestTokenAsync(
            form: new Dictionary<string, string> { ["grant_type"] = "client_credentials" },
            basic: (seeded.Client.clientId, seeded.Secret));

        var token = Assert.IsType<TokenSuccessResponse>(outcome.Issued);
        Assert.Equal(TestConfiguration.AccessTokenExpirationInMinutes * 60, token.ExpiresIn);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.AccessToken);
        var lifetime = jwt.ValidTo - jwt.ValidFrom;
        Assert.InRange(lifetime.TotalMinutes,
            TestConfiguration.AccessTokenExpirationInMinutes - 1,
            TestConfiguration.AccessTokenExpirationInMinutes + 1);
    }

    #endregion

    #region Refusals (RFC 6749 §5.2 error codes)

    [Fact]
    public async Task WrongSecret_IsInvalidClient()
    {
        var seeded = await OAuthFixtures.SeedConfidentialClientAsync();

        var outcome = await RequestTokenAsync(
            form: new Dictionary<string, string> { ["grant_type"] = "client_credentials" },
            basic: (seeded.Client.clientId, "not-the-secret"));

        Assert.Equal("invalid_client", outcome.Error?.Error);
    }

    [Fact]
    public async Task UnknownClient_IsInvalidClient()
    {
        var outcome = await RequestTokenAsync(
            form: new Dictionary<string, string> { ["grant_type"] = "client_credentials" },
            basic: ("cc-" + Guid.NewGuid().ToString("N"), "whatever"));

        Assert.Equal("invalid_client", outcome.Error?.Error);
    }

    [Fact]
    public async Task InactiveClient_IsRefused()
    {
        var seeded = await OAuthFixtures.SeedConfidentialClientAsync(isActive: false);

        var outcome = await RequestTokenAsync(
            form: new Dictionary<string, string> { ["grant_type"] = "client_credentials" },
            basic: (seeded.Client.clientId, seeded.Secret));

        Assert.Null(outcome.Issued);
        Assert.Equal("invalid_client", outcome.Error?.Error);
    }

    [Fact]
    public async Task ClientNotRegisteredForGrant_IsUnauthorizedClient()
    {
        var seeded = await OAuthFixtures.SeedConfidentialClientAsync(
            grantTypes: ClientCredential.GrantTypeValues.AuthorizationCode);

        var outcome = await RequestTokenAsync(
            form: new Dictionary<string, string> { ["grant_type"] = "client_credentials" },
            basic: (seeded.Client.clientId, seeded.Secret));

        Assert.Equal("unauthorized_client", outcome.Error?.Error);
    }

    [Fact]
    public async Task ScopeOutsideRegistration_IsInvalidScope()
    {
        var seeded = await OAuthFixtures.SeedConfidentialClientAsync();

        var outcome = await RequestTokenAsync(
            form: new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["scope"] = $"{ScopeProbe.GrantedScope} {ScopeProbe.UngrantedScope}",
            },
            basic: (seeded.Client.clientId, seeded.Secret));

        Assert.Equal("invalid_scope", outcome.Error?.Error);
        Assert.Contains(ScopeProbe.UngrantedScope, outcome.Error?.ErrorDescription);
    }

    [Fact]
    public async Task MissingGrantType_IsInvalidRequest()
    {
        var seeded = await OAuthFixtures.SeedConfidentialClientAsync();

        var outcome = await RequestTokenAsync(
            form: new Dictionary<string, string> { ["scope"] = ScopeProbe.GrantedScope },
            basic: (seeded.Client.clientId, seeded.Secret));

        Assert.Equal("invalid_request", outcome.Error?.Error);
    }

    [Fact]
    public async Task UnknownGrantType_IsUnsupportedGrantType()
    {
        var seeded = await OAuthFixtures.SeedConfidentialClientAsync();

        var outcome = await RequestTokenAsync(
            form: new Dictionary<string, string> { ["grant_type"] = "password" },
            basic: (seeded.Client.clientId, seeded.Secret));

        Assert.Equal("unsupported_grant_type", outcome.Error?.Error);
    }

    #endregion

    #region What the issued token can (and cannot) reach

    [Fact]
    public async Task IssuedToken_PassesRequiredScopeGateForGrantedScope()
    {
        var token = await IssueTokenAsync(scope: ScopeProbe.GrantedScope);

        var capture = await DispatchProbeAsync(nameof(ScopeProbe.RequiresGrantedScope), token);

        Assert.Equal("onAllowed", capture.BranchName);
    }

    [Fact]
    public async Task IssuedToken_FailsRequiredScopeGateForUngrantedScope()
    {
        var token = await IssueTokenAsync(scope: ScopeProbe.GrantedScope);

        var capture = await DispatchProbeAsync(nameof(ScopeProbe.RequiresUngrantedScope), token);

        Assert.Null(capture.BranchName);
        Assert.Equal(HttpStatusCode.Forbidden, capture.Response!.StatusCode);
        var challenge = Assert.Single(capture.Response.Headers["WWW-Authenticate"]);
        Assert.Contains("insufficient_scope", challenge);
        Assert.Contains($"scope=\"{ScopeProbe.UngrantedScope}\"", challenge);
    }

    /// <summary>
    /// Design assertion: a client-credentials token represents a MACHINE, not an
    /// account. It carries client_id + scp only, so role gates (<c>[SuperAdminClaim]</c>)
    /// must refuse it even though it is a valid first-party JWT.
    /// </summary>
    [Fact]
    public async Task IssuedToken_DoesNotSatisfyRoleGate()
    {
        var seeded = await OAuthFixtures.SeedConfidentialClientAsync();
        var token = await IssueTokenAsync(seeded, scope: ScopeProbe.GrantedScope);

        var method = typeof(ClientCredential).GetMethod(nameof(ClientCredential.GetByIdAsync))!;
        var request = OAuthRequests.Bare(HttpMethod.Get, "/api/OAuth/ClientCredential",
            bearer: token,
            query: new Dictionary<string, string> { ["id"] = seeded.Client.id.ToString() });

        var capture = await DispatchRawAsync(method, request);

        Assert.Null(capture.BranchName);
        Assert.Equal(HttpStatusCode.Forbidden, capture.Response!.StatusCode);
    }

    #endregion

    #region Secret rotation (dual secrets with a grace period)

    [Fact]
    public async Task RotateSecret_ReturnsPlaintextOnce_AndPreviousSecretWorksUntilRetired()
    {
        var seeded = await OAuthFixtures.SeedConfidentialClientAsync();

        var rotated = await RotateSecretAsync(seeded.Client.id);
        Assert.Equal(seeded.Client.clientId, rotated.ClientId);
        Assert.False(string.IsNullOrWhiteSpace(rotated.ClientSecret));
        Assert.NotEqual(seeded.Secret, rotated.ClientSecret);

        // the new secret authenticates
        Assert.IsType<TokenSuccessResponse>(
            (await RequestTokenAsync(seeded.Client.clientId, rotated.ClientSecret)).Issued);

        // grace period: the previous secret still authenticates until retired
        Assert.IsType<TokenSuccessResponse>(
            (await RequestTokenAsync(seeded.Client.clientId, seeded.Secret)).Issued);

        await RetireSecondarySecretAsync(seeded.Client.id);

        Assert.Equal("invalid_client",
            (await RequestTokenAsync(seeded.Client.clientId, seeded.Secret)).Error?.Error);
        Assert.IsType<TokenSuccessResponse>(
            (await RequestTokenAsync(seeded.Client.clientId, rotated.ClientSecret)).Issued);

        // "once": the stored row never exposes either secret (or hash) on the wire
        var stored = await seeded.Client.@ref.StorageGetAsync(c => c, () => default);
        var wire = JsonConvert.SerializeObject(stored);
        Assert.DoesNotContain(rotated.ClientSecret, wire);
        Assert.DoesNotContain(OAuthServer.ComputeSecretHash(rotated.ClientSecret), wire);
        Assert.DoesNotContain(OAuthServer.ComputeSecretHash(seeded.Secret), wire);
        Assert.DoesNotContain("\"client_secret\":", wire);
        Assert.DoesNotContain("secondary", wire, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SecondRotation_DropsTheOldestSecret()
    {
        var seeded = await OAuthFixtures.SeedConfidentialClientAsync();
        var secret0 = seeded.Secret;

        var secret1 = (await RotateSecretAsync(seeded.Client.id)).ClientSecret;
        var secret2 = (await RotateSecretAsync(seeded.Client.id)).ClientSecret;

        Assert.Equal("invalid_client",
            (await RequestTokenAsync(seeded.Client.clientId, secret0)).Error?.Error);
        Assert.IsType<TokenSuccessResponse>(
            (await RequestTokenAsync(seeded.Client.clientId, secret1)).Issued);
        Assert.IsType<TokenSuccessResponse>(
            (await RequestTokenAsync(seeded.Client.clientId, secret2)).Issued);
    }

    [Fact]
    public async Task RetireSecondary_WithNothingToRetire_IsIdempotent()
    {
        var seeded = await OAuthFixtures.SeedConfidentialClientAsync();

        await RetireSecondarySecretAsync(seeded.Client.id);

        Assert.IsType<TokenSuccessResponse>(
            (await RequestTokenAsync(seeded.Client.clientId, seeded.Secret)).Issued);
    }

    #endregion

    #region Drivers

    protected sealed record TokenOutcome(TokenSuccessResponse? Issued, OAuthTokenError? Error);

    protected Task<TokenOutcome> RequestTokenAsync(string clientId, string clientSecret) =>
        RequestTokenAsync(
            form: new Dictionary<string, string> { ["grant_type"] = "client_credentials" },
            basic: (clientId, clientSecret));

    protected async Task<TokenOutcome> RequestTokenAsync(
        IReadOnlyDictionary<string, string> form,
        (string clientId, string clientSecret)? basic = null)
    {
        var method = typeof(OAuthToken).GetMethod(nameof(OAuthToken.TokenAsync))!;
        var request = OAuthRequests.FormPost(OAuthRequests.TokenPath, form, basic);

        var capture = await DispatchRawAsync(method, request);

        if (capture.TryGet("onIssued", out var issuedArgs))
            return new TokenOutcome((TokenSuccessResponse?)issuedArgs[0], null);
        if (capture.TryGet("onError", out var errorArgs))
            return new TokenOutcome(null, (OAuthTokenError?)errorArgs[0]);
        throw new InvalidOperationException(
            $"Token endpoint fired no response branch; status={capture.Response?.StatusCode} " +
            $"reason={capture.Response?.ReasonPhrase}");
    }

    protected async Task<string> IssueTokenAsync(string scope)
    {
        var seeded = await OAuthFixtures.SeedConfidentialClientAsync();
        return await IssueTokenAsync(seeded, scope);
    }

    protected async Task<string> IssueTokenAsync(OAuthFixtures.SeededClient seeded, string scope)
    {
        var outcome = await RequestTokenAsync(
            form: new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["scope"] = scope,
            },
            basic: (seeded.Client.clientId, seeded.Secret));
        var token = Assert.IsType<TokenSuccessResponse>(outcome.Issued);
        return token.AccessToken;
    }

    protected Task<ResponseBranchCapture> DispatchProbeAsync(string probeMethodName, string bearer)
    {
        var method = typeof(ScopeProbe).GetMethod(probeMethodName)!;
        var request = OAuthRequests.Bare(HttpMethod.Get, "/api/scope-probe", bearer);
        return DispatchRawAsync(method, request);
    }

    protected async Task<ClientSecretResponse> RotateSecretAsync(Guid clientRecordId)
    {
        var method = typeof(ClientCredential).GetMethod(nameof(ClientCredential.RotateSecretAsync))!;
        var request = OAuthRequests.Bare(HttpMethod.Post, "/api/OAuth/ClientCredential/rotate-secret",
            bearer: OAuthFixtures.SuperAdminToken(),
            query: new Dictionary<string, string> { ["id"] = clientRecordId.ToString() });

        var capture = await DispatchRawAsync(method, request);

        Assert.True(capture.TryGet("onRotated", out var args),
            $"rotate-secret did not rotate; branch={capture.BranchName} status={capture.Response?.StatusCode} reason={capture.Response?.ReasonPhrase}");
        return Assert.IsType<ClientSecretResponse>(args[0]);
    }

    protected async Task<ClientCredential> RetireSecondarySecretAsync(Guid clientRecordId)
    {
        var method = typeof(ClientCredential).GetMethod(nameof(ClientCredential.RetireSecondarySecretAsync))!;
        var request = OAuthRequests.Bare(HttpMethod.Post, "/api/OAuth/ClientCredential/retire-secondary",
            bearer: OAuthFixtures.SuperAdminToken(),
            query: new Dictionary<string, string> { ["id"] = clientRecordId.ToString() });

        var capture = await DispatchRawAsync(method, request);

        Assert.True(capture.TryGet("onRetired", out var args),
            $"retire-secondary did not retire; branch={capture.BranchName} status={capture.Response?.StatusCode} reason={capture.Response?.ReasonPhrase}");
        return Assert.IsType<ClientCredential>(args[0]);
    }

    #endregion
}
