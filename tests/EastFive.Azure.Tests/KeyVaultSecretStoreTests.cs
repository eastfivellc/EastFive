using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Azure;
using Azure.Security.KeyVault.Secrets;
using Xunit;

using EastFive.Azure.Secrets;
using EastFive.Configuration;

namespace EastFive.Azure.Tests;

/// <summary>
/// The vault-backed secret store over a fake <see cref="SecretClient"/>: vault order, 404 as
/// missing vs any other status as failure, per-name caching with a reload interval, failures
/// never cached, and names that are not vault names never reaching a client.
/// </summary>
public class KeyVaultSecretStoreTests
{
    private sealed class FakeVault : SecretClient
    {
        public readonly Dictionary<string, string> Secrets = new(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> Asked = new();
        public int? FailWith;

        public override Task<Response<KeyVaultSecret>> GetSecretAsync(string name, string? version = null, CancellationToken cancellationToken = default)
        {
            Asked.Add(name);
            if (FailWith is { } status)
                throw new RequestFailedException(status, "denied: k-should-never-leak");
            if (!Secrets.TryGetValue(name, out var value))
                throw new RequestFailedException(404, "SecretNotFound");
            return Task.FromResult(Response.FromValue(new KeyVaultSecret(name, value), null!));
        }
    }

    private sealed class ManualTime : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static readonly Uri First = new("https://first.vault.azure.net/");
    private static readonly Uri Second = new("https://second.vault.azure.net/");

    private static Task<string> Ask(ISecretStore store, string name)
        => store.GetSecretAsync(name,
            (secret, source) => $"found:{secret.Reveal()}@{source.Location}",
            () => "missing",
            why => $"failure:{why}");

    [Fact]
    public async Task VaultsAreConsultedInOrder_404FallsThrough()
    {
        var first = new FakeVault();
        var second = new FakeVault();
        first.Secrets["Shared-Key"] = "from-first";
        second.Secrets["Shared-Key"] = "from-second";
        second.Secrets["Only-Second"] = "second-only";
        var store = new KeyVaultSecretStore(new[] { (First, (SecretClient)first), (Second, (SecretClient)second) });

        Assert.Equal("found:from-first@first.vault.azure.net", await Ask(store, "Shared-Key"));
        Assert.Empty(second.Asked);
        Assert.Equal("found:second-only@second.vault.azure.net", await Ask(store, "Only-Second"));
        Assert.Equal("missing", await Ask(store, "Nowhere"));
        Assert.Equal(new[] { "first.vault.azure.net", "second.vault.azure.net" },
            new[] { store.Sources[0].Location, store.Sources[1].Location });
    }

    [Fact]
    public async Task NonNotFoundErrors_AreFailures_ThatStopTheWalk_AndCarryNoSdkText()
    {
        var first = new FakeVault { FailWith = 403 };
        var second = new FakeVault();
        second.Secrets["A-Key"] = "second";
        var store = new KeyVaultSecretStore(new[] { (First, (SecretClient)first), (Second, (SecretClient)second) });

        var outcome = await Ask(store, "A-Key");
        Assert.Equal("failure:Vault first.vault.azure.net answered 403 for [A-Key].", outcome);
        Assert.DoesNotContain("k-should-never-leak", outcome);
        Assert.Empty(second.Asked);

        // Failures are not cached: the next read after recovery answers.
        first.FailWith = null;
        Assert.Equal("found:second@second.vault.azure.net", await Ask(store, "A-Key"));
    }

    [Fact]
    public async Task FoundAndMissing_AreCachedPerName_ForTheReloadInterval()
    {
        var vault = new FakeVault();
        vault.Secrets["A-Key"] = "v1";
        var time = new ManualTime();
        var store = new KeyVaultSecretStore(new[] { (First, (SecretClient)vault) }, TimeSpan.FromMinutes(30), time);

        Assert.Equal("found:v1@first.vault.azure.net", await Ask(store, "A-Key"));
        Assert.Equal("missing", await Ask(store, "B-Key"));
        vault.Secrets["A-Key"] = "v2";
        vault.Secrets["B-Key"] = "b";
        time.Now += TimeSpan.FromMinutes(29);
        Assert.Equal("found:v1@first.vault.azure.net", await Ask(store, "A-Key"));
        Assert.Equal("missing", await Ask(store, "B-Key"));
        Assert.Equal(new[] { "A-Key", "B-Key" }, vault.Asked);

        time.Now += TimeSpan.FromMinutes(2);
        Assert.Equal("found:v2@first.vault.azure.net", await Ask(store, "A-Key"));
        Assert.Equal("found:b@first.vault.azure.net", await Ask(store, "B-Key"));
    }

    [Fact]
    public async Task InvalidNames_NeverReachAClient()
    {
        var vault = new FakeVault();
        var store = new KeyVaultSecretStore(new[] { (First, (SecretClient)vault) });
        Assert.StartsWith("failure:", await Ask(store, "Azure.Storage.Account.dev"));
        Assert.Empty(vault.Asked);
    }

    [Fact]
    public void From_SkipsEntriesWithoutUrlOrCredential_NamingOnlyTheUrl()
    {
        var unusable = new List<string>();
        var store = KeyVaultSecretStore.From(new[]
        {
            new KeyVaultAccess { Url = "https://ok.vault.azure.net/", TenantId = "t", ClientId = "c", ClientSecret = "k-should-never-leak" },
            new KeyVaultAccess { Url = "https://nocred.vault.azure.net/" },
            new KeyVaultAccess { TenantId = "t", ClientId = "c", ClientSecret = "s" },
            new KeyVaultAccess { Url = "http://insecure.vault.azure.net/", ManagedIdentity = true },
        }, unusable.Add);
        Assert.Equal(new[] { "ok.vault.azure.net" }, new[] { store.Sources[0].Location });
        Assert.Single(store.Sources);
        Assert.Equal(new[] { "https://nocred.vault.azure.net/", "(no url)", "http://insecure.vault.azure.net/" }, unusable);
    }

    [Fact]
    public async Task OverridesThenVaults_LocalOverrideWins_AndNamesItsSource()
    {
        var vault = new FakeVault();
        vault.Secrets["AzureStorage-dev-ConnectionString"] = "from-vault";
        var values = new Dictionary<string, string> { ["Secrets:AzureStorage-dev-ConnectionString"] = "from-local" };
        var store = new CompositeSecretStore(
            new ConfigurationSecretOverrides(key => values.GetValueOrDefault(key)),
            new KeyVaultSecretStore(new[] { (First, (SecretClient)vault) }));
        Assert.Equal("found:from-local@Secrets", await Ask(store, "AzureStorage-dev-ConnectionString"));
        Assert.Empty(vault.Asked);
        values.Clear();
        Assert.Equal("found:from-vault@first.vault.azure.net", await Ask(store, "AzureStorage-dev-ConnectionString"));
    }
}
