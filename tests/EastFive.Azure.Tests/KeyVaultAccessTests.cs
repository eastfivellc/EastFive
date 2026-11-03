using System;
using System.Collections.Generic;

using Azure.Identity;
using Xunit;

using EastFive.Azure.Secrets;

namespace EastFive.Azure.Tests;

/// <summary>
/// One <see cref="KeyVaultAccess"/> entry: which credential each declared mode builds, the
/// mixed-mode manifest error and the deployed-host override. Every id below is fabricated.
/// </summary>
public class KeyVaultAccessTests
{
    private const string Url = "https://unit.vault.azure.net/";
    private const string Tenant = "00000000-0000-0000-0000-00000000000a";
    private const string Client = "00000000-0000-0000-0000-00000000000b";

    private static string Built(KeyVaultAccess access, bool deployed, List<string>? warnings = null)
        => access.CreateCredential(
            (credential, mode) => $"{mode}:{credential.GetType().Name}",
            () => "none",
            problem => $"invalid:{problem}",
            warnings == null ? null : warnings.Add,
            deployed);

    [Fact]
    public void EachDeclaredMode_BuildsItsCredential()
    {
        Assert.Equal($"ClientSecret:{nameof(ClientSecretCredential)}",
            Built(new KeyVaultAccess { Url = Url, TenantId = Tenant, ClientId = Client, ClientSecret = "k-fabricated" }, deployed: false));
        Assert.Equal($"ManagedIdentity:{nameof(ManagedIdentityCredential)}",
            Built(new KeyVaultAccess { Url = Url, ManagedIdentity = true }, deployed: false));
        Assert.Equal($"AzureCli:{nameof(AzureCliCredential)}",
            Built(new KeyVaultAccess { Url = Url, UseAzureCli = true }, deployed: false));
        Assert.Equal($"AzureCli:{nameof(AzureCliCredential)}",
            Built(new KeyVaultAccess { Url = Url, UseAzureCli = true, TenantId = Tenant }, deployed: false));
        Assert.Equal("none", Built(new KeyVaultAccess { Url = Url }, deployed: false));
        // An incomplete service principal is not a declared mode that works; it reads as none.
        Assert.Equal("none", Built(new KeyVaultAccess { Url = Url, TenantId = Tenant, ClientId = Client }, deployed: false));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, false)]
    public void MixedModes_AreAManifestError_NamingTheVault(bool secret, bool managedIdentity, bool azureCli)
    {
        var access = new KeyVaultAccess
        {
            Url = Url,
            TenantId = Tenant,
            ClientId = secret ? Client : null,
            ClientSecret = secret ? "k-should-never-leak" : null,
            ManagedIdentity = managedIdentity,
            UseAzureCli = azureCli,
        };
        var outcome = Built(access, deployed: false);
        Assert.StartsWith($"invalid:Vault {Url} declares more than one access mode", outcome);
        Assert.DoesNotContain("k-should-never-leak", outcome);
        // Deployed or not, a mixed entry stays an error rather than guessing.
        Assert.StartsWith("invalid:", Built(access, deployed: true));
    }

    [Fact]
    public void Deployed_IgnoresAzureCli_WithAWarning_AndUsesManagedIdentity()
    {
        var warnings = new List<string>();
        Assert.Equal($"ManagedIdentity:{nameof(ManagedIdentityCredential)}",
            Built(new KeyVaultAccess { Url = Url, UseAzureCli = true }, deployed: true, warnings));
        Assert.Equal($"Vault {Url} asks for Azure CLI sign-in, which never applies on a deployed host; using managed identity.",
            Assert.Single(warnings));

        Assert.Equal($"ManagedIdentity:{nameof(ManagedIdentityCredential)}", Built(new KeyVaultAccess { Url = Url }, deployed: true));
        Assert.Equal($"ClientSecret:{nameof(ClientSecretCredential)}",
            Built(new KeyVaultAccess { Url = Url, TenantId = Tenant, ClientId = Client, ClientSecret = "k-fabricated" }, deployed: true));
    }

    [Fact]
    public void From_ReportsAMixedEntry_AsUnusable_WithItsReason()
    {
        var unusable = new List<string>();
        var warnings = new List<string>();
        var store = KeyVaultSecretStore.From(new[]
        {
            new KeyVaultAccess { Url = Url, ManagedIdentity = true, UseAzureCli = true },
            new KeyVaultAccess { Url = "https://cli.vault.azure.net/", UseAzureCli = true },
        }, unusable.Add, onWarning: warnings.Add);
        Assert.Equal(new[] { Url }, unusable);
        Assert.Contains("declares more than one access mode", Assert.Single(warnings));
        Assert.Equal("cli.vault.azure.net", Assert.Single(store.Sources).Location);
    }
}
