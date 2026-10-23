using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Xunit;

using EastFive.Configuration;

namespace EastFive.Api.Tests;

/// <summary>
/// The secret store's outcome semantics: local overrides by name, composite precedence
/// (first found wins, missing falls through, failure stops), and refusal of non-vault names.
/// </summary>
public class SecretStoreTests
{
    private static Task<string> Ask(ISecretStore store, string name)
        => store.GetSecretAsync(name,
            (secret, source) => $"found:{secret.Reveal()}@{source.Kind}",
            () => "missing",
            why => $"failure:{why}");

    private sealed class Throwing : ISecretStore
    {
        public int Asked;
        public IReadOnlyList<SecretSource> Sources => new[] { new SecretSource(SecretSource.Vault, "down.vault.azure.net") };
        public Task<TResult> GetSecretAsync<TResult>(string name, Func<Secret, SecretSource, TResult> onFound,
            Func<TResult> onMissing, Func<string, TResult> onFailure)
        {
            Asked++;
            return Task.FromResult(onFailure("Vault down.vault.azure.net answered 503."));
        }
    }

    [Fact]
    public async Task Overrides_ReadTheSecretsSectionByName()
    {
        var reads = new List<string>();
        var values = new Dictionary<string, string> { ["Secrets:Telnyx-acme-ApiKey"] = " k-acme " };
        var overrides = new ConfigurationSecretOverrides(key => { reads.Add(key); return values.GetValueOrDefault(key); });
        Assert.Equal("found:k-acme@local override", await Ask(overrides, "Telnyx-acme-ApiKey"));
        Assert.Equal("missing", await Ask(overrides, "Telnyx-other-ApiKey"));
        Assert.Equal(new[] { "Secrets:Telnyx-acme-ApiKey", "Secrets:Telnyx-other-ApiKey" }, reads);
    }

    [Fact]
    public async Task Overrides_RefuseNonVaultNames_AndReportReaderFailures()
    {
        var reads = new List<string>();
        var overrides = new ConfigurationSecretOverrides(key => { reads.Add(key); return "x"; });
        Assert.StartsWith("failure:", await Ask(overrides, "Azure.Storage.Account.dev"));
        Assert.StartsWith("failure:", await Ask(overrides, "a:b"));
        Assert.Empty(reads);

        var broken = new ConfigurationSecretOverrides(_ => throw new InvalidOperationException("k-acme"));
        var outcome = await Ask(broken, "Telnyx-acme-ApiKey");
        Assert.StartsWith("failure:", outcome);
        Assert.DoesNotContain("k-acme", outcome);
    }

    [Fact]
    public async Task Composite_FirstFoundWins_MissingFallsThrough()
    {
        var local = new InMemorySecretStore(new Dictionary<string, string> { ["A-Key"] = "local" }, "local");
        var vault = new InMemorySecretStore(new Dictionary<string, string> { ["A-Key"] = "vault", ["B-Key"] = "vault-b" }, "vault");
        var store = new CompositeSecretStore(local, vault);
        Assert.Equal("found:local@draft", await Ask(store, "A-Key"));
        Assert.Equal("found:vault-b@draft", await Ask(store, "B-Key"));
        Assert.Equal("missing", await Ask(store, "C-Key"));
        Assert.Equal(new[] { "local", "vault" }, new[] { store.Sources[0].Location, store.Sources[1].Location });
    }

    [Fact]
    public async Task Composite_FailureStopsTheWalk_NeverMissing()
    {
        var down = new Throwing();
        var later = new InMemorySecretStore(new Dictionary<string, string> { ["A-Key"] = "later" });
        var store = new CompositeSecretStore(new InMemorySecretStore(new Dictionary<string, string>()), down, later);
        Assert.Equal("failure:Vault down.vault.azure.net answered 503.", await Ask(store, "A-Key"));
        Assert.Equal(1, down.Asked);

        // An earlier hit never consults the failing store.
        var shadowed = new CompositeSecretStore(new InMemorySecretStore(new Dictionary<string, string> { ["A-Key"] = "first" }), down);
        Assert.Equal("found:first@draft", await Ask(shadowed, "A-Key"));
        Assert.Equal(1, down.Asked);
    }

    [Fact]
    public async Task AmbientStore_DefaultsToLocalOverridesOnly()
    {
        Assert.Contains(SecretStore.Current.Sources, source => source.Kind == SecretSource.LocalOverride);
        Assert.DoesNotContain(SecretStore.AmbientOverrides.Sources, source => source.Kind == SecretSource.Vault);
        Assert.Equal("found:x@draft", await Ask(new InMemorySecretStore(new Dictionary<string, string> { ["Z-Key"] = "x" }), "Z-Key"));
    }
}
