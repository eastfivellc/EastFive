using System.Net;

using Xunit;

using EastFive.Api;
using EastFive.Api.Tests.Harness;
using EastFive.Azure.OAuth;
using EastFive.Azure.Persistence.AzureStorageTables;

namespace EastFive.Azure.Tests.OAuth;

public class ClientCredentialPatchTests : TestSession
{
    [Theory]
    [InlineData("""{"scope":"rosemary.read"}""", "rosemary.read")]
    [InlineData("{}", OAuthFixtures.RegisteredScope)]
    [InlineData("""{"scope":null}""", null)]
    [InlineData("""{"scope":"rosemary.read","client_id":"replaced","client_secret":"replaced","clientSecretSecondary":"replaced","created_at":"2000-01-01T00:00:00Z","updated_at":"2000-01-01T00:00:00Z"}""", "rosemary.read")]
    public async Task Patch_PersistsScopeAndPreservesProtectedAndOmittedFields(
        string json, string? expectedScope)
    {
        var seeded = await OAuthFixtures.SeedConfidentialClientAsync();
        var capture = await PatchClientAsync(seeded.Client.id, json);

        Assert.True(capture.BranchName == "onUpdated",
            $"PATCH failed: status={capture.Response?.StatusCode} reason={capture.Response?.ReasonPhrase}");
        Assert.True(capture.TryGet("onUpdated", out var args));
        var updated = Assert.IsType<ClientCredential>(args[0]);
        Assert.Equal(expectedScope, updated.scope);

        var stored = await seeded.Client.@ref.StorageGetAsync(
            client => client,
            () => throw new InvalidOperationException("Updated client was not found."));
        Assert.Equal(expectedScope, stored.scope);
        Assert.Equal(seeded.Client.clientId, stored.clientId);
        Assert.Equal(seeded.Client.clientType, stored.clientType);
        Assert.Equal(seeded.Client.clientSecret, stored.clientSecret);
        Assert.True(string.IsNullOrEmpty(stored.clientSecretSecondary));
        Assert.Equal(seeded.Client.name, stored.name);
        Assert.Equal(seeded.Client.grantTypes, stored.grantTypes);
        Assert.Equal(seeded.Client.tokenEndpointAuthMethod, stored.tokenEndpointAuthMethod);
        Assert.Equal(seeded.Client.isActive, stored.isActive);
        Assert.Equal(seeded.Client.createdAt, stored.createdAt);
        Assert.True(stored.updatedAt >= seeded.Client.updatedAt);
    }

    [Fact]
    public async Task Patch_UpdatesPublicClientScopes()
    {
        var seeded = await OAuthFixtures.SeedConfidentialClientAsync();
        await seeded.Client.@ref.StorageUpdateAsync2(
            client =>
            {
                client.clientType = ClientCredential.ClientTypes.Public;
                client.clientSecret = null;
                client.grantTypes = ClientCredential.GrantTypeValues.AuthorizationCode;
                client.tokenEndpointAuthMethod = ClientCredential.TokenEndpointAuthMethods.None;
                client.redirectUris = "https://example.test/callback";
                return client;
            },
            client => client,
            () => throw new InvalidOperationException("Seeded client was not found."));

        var capture = await PatchClientAsync(seeded.Client.id, """{"scope":"rosemary.read"}""");

        Assert.True(capture.TryGet("onUpdated", out var args));
        var updated = Assert.IsType<ClientCredential>(args[0]);
        Assert.Equal("rosemary.read", updated.scope);
        Assert.Equal(ClientCredential.ClientTypes.Public, updated.clientType);
        Assert.Equal("https://example.test/callback", updated.redirectUris);
        Assert.True(string.IsNullOrEmpty(updated.clientSecret));
    }

    [Fact]
    public async Task Patch_RejectsMalformedFieldWithoutChangingStoredScope()
    {
        var seeded = await OAuthFixtures.SeedConfidentialClientAsync();

        var capture = await PatchClientAsync(seeded.Client.id,
            """{"scope":"rosemary.read","is_active":"not-a-boolean"}""");

        Assert.Equal(HttpStatusCode.BadRequest, capture.Response!.StatusCode);
        var stored = await seeded.Client.@ref.StorageGetAsync(
            client => client,
            () => throw new InvalidOperationException("Seeded client was not found."));
        Assert.Equal(OAuthFixtures.RegisteredScope, stored.scope);
    }

    [Fact]
    public async Task Patch_UnknownClientReturnsNotFound()
    {
        var capture = await PatchClientAsync(Guid.NewGuid(), """{"scope":"rosemary.read"}""");

        Assert.Equal("onNotFound", capture.BranchName);
    }

    [Fact]
    public async Task Patch_RequiresSuperAdmin()
    {
        var seeded = await OAuthFixtures.SeedConfidentialClientAsync();
        var method = typeof(ClientCredential).GetMethod(nameof(ClientCredential.UpdateAsync))!;
        var request = BuildJsonRequest(method, """{"scope":"rosemary.read"}""",
            new Dictionary<string, object> { ["id"] = seeded.Client.id });

        var capture = await DispatchRawAsync(method, request);

        Assert.Equal(HttpStatusCode.Forbidden, capture.Response!.StatusCode);
        var stored = await seeded.Client.@ref.StorageGetAsync(
            client => client,
            () => throw new InvalidOperationException("Seeded client was not found."));
        Assert.Equal(OAuthFixtures.RegisteredScope, stored.scope);
    }

    private Task<ResponseBranchCapture> PatchClientAsync(Guid id, string json)
    {
        var method = typeof(ClientCredential).GetMethod(nameof(ClientCredential.UpdateAsync))!;
        var request = BuildJsonRequest(method, json,
            new Dictionary<string, object> { ["id"] = id });
        request.Headers["Authorization"] = new[] { $"Bearer {OAuthFixtures.SuperAdminToken()}" };
        return DispatchRawAsync(method, request);
    }
}
