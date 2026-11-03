using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

using Azure;
using Azure.Core;
using Azure.Identity;
using Xunit;

using EastFive.Azure.Secrets;
using EastFive.Configuration;

namespace EastFive.Azure.Tests;

/// <summary>
/// One <see cref="KeyVaultAccess"/> entry: which credential each declared mode builds, the
/// mixed-mode manifest error, the deployed-host override, and how every SDK failure a vault read
/// or token request can throw is classified. Every id below is fabricated.
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

    [Theory]
    [InlineData("7000222", "the client secret has expired")]
    [InlineData("7000215", "the client secret is invalid")]
    [InlineData("700016", "the application (client id) was not found in the tenant")]
    [InlineData("90002", "the tenant was not found")]
    public void AuthenticationFailures_KeepTheAadstsCode(string code, string reason)
    {
        var error = new AuthenticationFailedException(
            $"ClientSecretCredential authentication failed: AADSTS{code}: something about app '{Client}' in tenant '{Tenant}'. Trace ID: 00000000-0000-0000-0000-0000000000ff");
        var failure = KeyVaultFailures.Classify(error, "unit.vault.azure.net")!;
        Assert.Equal(SecretFailureKind.AuthFailed, failure.Kind);
        Assert.Equal(code, failure.Aadsts);
        Assert.Equal($"Vault unit.vault.azure.net: sign-in was refused: {reason} (AADSTS{code}).", failure.Message);
        Assert.DoesNotContain(Client, failure.Message);
        Assert.DoesNotContain(Tenant, failure.Message);
    }

    [Fact]
    public void AnAadstsCode_IsFoundOnAnInnerException()
    {
        var error = new AuthenticationFailedException("ClientSecretCredential authentication failed",
            new InvalidOperationException("AADSTS7000222: The provided client secret keys are expired."));
        Assert.Equal("7000222", KeyVaultFailures.Classify(error, "unit.vault.azure.net")!.Aadsts);
    }

    [Fact]
    public void Forbidden_KeepsTheServiceCode()
    {
        var failure = KeyVaultFailures.Classify(
            new RequestFailedException(403, "Caller is not authorized to perform action on resource.", "ForbiddenByRbac", null),
            "unit.vault.azure.net", "A-Key")!;
        Assert.Equal(SecretFailureKind.Forbidden, failure.Kind);
        Assert.Equal("forbidden", failure.KindName);
        Assert.Equal(403, failure.Status);
        Assert.Equal("ForbiddenByRbac", failure.Code);
        Assert.Equal("Vault unit.vault.azure.net denied the read for [A-Key] (403 ForbiddenByRbac): the signed-in principal has no secret read access.",
            failure.Message);
    }

    [Fact]
    public void CredentialUnavailable_IsItsOwnKind_AndScrubbed()
    {
        var failure = KeyVaultFailures.Classify(
            new CredentialUnavailableException("Please run 'az login' to set up account. client_secret=abc~fabricated-not-real-0123456789\nsecond line"),
            "unit.vault.azure.net")!;
        Assert.Equal(SecretFailureKind.CredentialUnavailable, failure.Kind);
        Assert.Equal("credential_unavailable", failure.KindName);
        Assert.Contains("Please run 'az login' to set up account.", failure.Message);
        Assert.DoesNotContain("abc~fabricated", failure.Message);
        Assert.DoesNotContain("second line", failure.Message);
    }

    [Fact]
    public void Unreachable_IsNetwork()
    {
        Assert.Equal(SecretFailureKind.Network,
            KeyVaultFailures.Classify(new HttpRequestException("No such host is known."), "unit.vault.azure.net")!.Kind);
        Assert.Equal(SecretFailureKind.Network,
            KeyVaultFailures.Classify(new RequestFailedException(0, "No such host", null, new HttpRequestException("No such host")), "unit.vault.azure.net")!.Kind);
        Assert.Equal(SecretFailureKind.Network,
            KeyVaultFailures.Classify(new TaskCanceledException(), "unit.vault.azure.net")!.Kind);
    }

    [Fact]
    public void OtherStatuses_AreOther_AndUnknownExceptionsAreNotClassified()
    {
        var failure = KeyVaultFailures.Classify(new RequestFailedException(429, "Too many requests", "Throttled", null), "unit.vault.azure.net")!;
        Assert.Equal(SecretFailureKind.Other, failure.Kind);
        Assert.Equal(429, failure.Status);
        Assert.Equal("Vault unit.vault.azure.net answered 429 Throttled.", failure.Message);
        Assert.Null(KeyVaultFailures.Classify(new InvalidOperationException("bug"), "unit.vault.azure.net"));
    }
}
