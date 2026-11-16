using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

using EastFive.Configuration;

namespace EastFive.Api.Tests;

/// <summary>
/// Keyed instances are loaded at the point of use and cached by (type, instance key), ignoring
/// case: the first use loads once however many callers arrive together, the synchronous read never
/// loads, a loaded configuration stays until refreshed, an ISSUE is re-read after its lifetime (so
/// a secret added to the vault is picked up without a restart and a missing one is not read on
/// every request), and a refresh that cannot reach the source keeps the last good outcome.
/// </summary>
public class ConfigurationInstanceCacheTests
{
    [ConfigurationType("Tenant", Label = "Tenant", Description = "Per-tenant credentials.")]
    private sealed class Tenant : IProvideConfiguration, IProvideConfigurationInstanceKey
    {
        public Tenant(string name) => InstanceKey = name;

        public string InstanceKey { get; }

        [ConfigurationMember("Tenant.{key}.Region", Kind = ConfigurationMemberKind.Defaulted, Default = "us", Label = "Region", Description = "Where it runs.")]
        public string Region { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "API key", Description = "Authenticates the tenant.")]
        public Secret ApiKey { get; set; } = default!;
    }

    [ConfigurationType("Other", Label = "Other", Description = "Another keyed type.")]
    private sealed class Other : IProvideConfiguration, IProvideConfigurationInstanceKey
    {
        public Other(string name) => InstanceKey = name;

        public string InstanceKey { get; }

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "API key", Description = "Authenticates.")]
        public Secret ApiKey { get; set; } = default!;
    }

    /// <summary>A store a test changes under the cache: found, missing or down, optionally held
    /// open so callers pile up on one read.</summary>
    private sealed class Vault : ISecretStore
    {
        public readonly Dictionary<string, string> Secrets = new(StringComparer.OrdinalIgnoreCase);
        public bool Down;
        public TaskCompletionSource? Gate;
        private int asked;

        public int Asked => Volatile.Read(ref asked);

        public IReadOnlyList<SecretSource> Sources { get; } = new[] { new SecretSource(SecretSource.Vault, "test") };

        public async Task<TResult> GetSecretAsync<TResult>(string name, Func<Secret, SecretSource, TResult> onFound,
            Func<TResult> onMissing, Func<SecretFailure, TResult> onFailure)
        {
            Interlocked.Increment(ref asked);
            if (Gate is { } gate)
                await gate.Task.ConfigureAwait(false);
            return Down ? onFailure(SecretFailure.Other("The vault is down."))
                : Secrets.TryGetValue(name, out var text) ? onFound(new Secret(text), Sources[0])
                : onMissing();
        }
    }

    private sealed class Clock
    {
        public DateTimeOffset Now = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);
        public void Advance(TimeSpan by) => Now += by;
    }

    private static (ConfigurationInstanceCache cache, Vault vault, Clock clock) Cache()
    {
        var vault = new Vault();
        var clock = new Clock();
        return (new ConfigurationInstanceCache(_ => null, () => vault, () => clock.Now), vault, clock);
    }

    private static Task<string> Use(ConfigurationInstanceCache cache, string name)
        => cache.GetOrLoadAsync(() => new Tenant(name),
            tenant => $"{tenant.InstanceKey}:{tenant.Region}:{tenant.ApiKey.Reveal()}",
            issue => $"{issue.Category}:{issue.Key}");

    private static string Read(ConfigurationInstanceCache cache, string name)
        => cache.Get<Tenant, string>(name,
            tenant => $"{tenant.InstanceKey}:{tenant.Region}:{tenant.ApiKey.Reveal()}",
            issue => $"{issue.Category}:{issue.Key}");

    [Fact]
    public async Task FirstUse_LoadsOnce_HoweverManyCallersArriveTogether()
    {
        var (cache, vault, _) = Cache();
        vault.Secrets["Tenant-east-ApiKey"] = "k-east";
        vault.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var callers = Enumerable.Range(0, 25).Select(_ => Task.Run(() => Use(cache, "east"))).ToArray();
        while (vault.Asked == 0)
            await Task.Delay(5);
        await Task.Delay(50);
        vault.Gate.SetResult();

        Assert.All(await Task.WhenAll(callers), outcome => Assert.Equal("east:us:k-east", outcome));
        Assert.Equal(1, vault.Asked);

        vault.Gate = null;
        Assert.Equal("east:us:k-east", await Use(cache, "east"));
        Assert.Equal(1, vault.Asked);
    }

    [Fact]
    public async Task EachInstance_AndEachType_IsItsOwnEntry_IgnoringCase()
    {
        var (cache, vault, _) = Cache();
        vault.Secrets["Tenant-east-ApiKey"] = "k-east";
        vault.Secrets["Tenant-west-ApiKey"] = "k-west";
        vault.Secrets["Other-east-ApiKey"] = "k-other";

        Assert.Equal("east:us:k-east", await Use(cache, "east"));
        Assert.Equal("west:us:k-west", await Use(cache, "west"));
        Assert.Equal("k-other", await cache.GetOrLoadAsync(() => new Other("east"), other => other.ApiKey.Reveal(), issue => issue.ToString()));
        Assert.Equal(3, vault.Asked);

        // A vault ignores the case of a secret name, so EAST is the instance east.
        Assert.Equal("east:us:k-east", await Use(cache, "EAST"));
        Assert.Equal("east:us:k-east", Read(cache, "East"));
        Assert.Equal(3, vault.Asked);
        Assert.True(cache.IsLoaded(typeof(Tenant), "EAST"));
        Assert.False(cache.IsLoaded(typeof(Other), "west"));
    }

    [Fact]
    public async Task TheSynchronousRead_NeverLoads()
    {
        var (cache, vault, _) = Cache();
        vault.Secrets["Tenant-east-ApiKey"] = "k-east";

        Assert.Equal("NotLoaded:Tenant", Read(cache, "east"));
        Assert.Equal(0, vault.Asked);

        await Use(cache, "east");
        Assert.Equal("east:us:k-east", Read(cache, "east"));
        Assert.Equal(1, vault.Asked);
    }

    [Fact]
    public async Task AnIssue_IsReReadAfterItsLifetime_ALoadedConfigurationIsNot()
    {
        var (cache, vault, clock) = Cache();

        Assert.Equal("Missing:Tenant-east-ApiKey", await Use(cache, "east"));
        Assert.Equal("Missing:Tenant-east-ApiKey", Read(cache, "east"));
        Assert.Equal(1, vault.Asked);

        // The secret arrives; within the issue's lifetime the vault is not asked again.
        vault.Secrets["Tenant-east-ApiKey"] = "k-east";
        clock.Advance(ConfigurationInstanceCache.DefaultIssueLifetime - TimeSpan.FromSeconds(1));
        Assert.Equal("Missing:Tenant-east-ApiKey", await Use(cache, "east"));
        Assert.Equal(1, vault.Asked);

        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal("east:us:k-east", await Use(cache, "east"));
        Assert.Equal(2, vault.Asked);

        vault.Secrets["Tenant-east-ApiKey"] = "k-rotated";
        clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal("east:us:k-east", await Use(cache, "east"));
        Assert.Equal(2, vault.Asked);
    }

    [Fact]
    public async Task AnInvalidInstanceKey_IsAnIssue_WithoutAskingTheVault()
    {
        var (cache, vault, _) = Cache();
        Assert.Equal("Invalid:InstanceKey", await Use(cache, "wh_abc"));
        Assert.Equal(0, vault.Asked);
    }

    [Fact]
    public async Task Forget_DropsOneInstance()
    {
        var (cache, vault, _) = Cache();
        vault.Secrets["Tenant-east-ApiKey"] = "k-east";
        vault.Secrets["Tenant-west-ApiKey"] = "k-west";
        await Use(cache, "east");
        await Use(cache, "west");

        Assert.True(cache.Forget(typeof(Tenant), "EAST"));
        Assert.False(cache.Forget(typeof(Tenant), "east"));
        Assert.Equal("NotLoaded:Tenant", Read(cache, "east"));
        Assert.Equal("west:us:k-west", Read(cache, "west"));

        vault.Secrets["Tenant-east-ApiKey"] = "k-again";
        Assert.Equal("east:us:k-again", await Use(cache, "east"));
        Assert.Equal(3, vault.Asked);
    }

    [Fact]
    public async Task Forget_DuringALoad_LeavesNothingCached()
    {
        var (cache, vault, _) = Cache();
        vault.Secrets["Tenant-east-ApiKey"] = "k-east";
        vault.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var caller = Use(cache, "east");
        while (vault.Asked == 0)
            await Task.Delay(5);
        Assert.False(cache.Forget(typeof(Tenant), "east"));
        vault.Gate.SetResult();

        // The caller that was already waiting gets its answer; the forgotten instance stays forgotten.
        Assert.Equal("east:us:k-east", await caller);
        Assert.Equal("NotLoaded:Tenant", Read(cache, "east"));
    }

    [Fact]
    public async Task Refresh_ReReadsEveryInstance_AndKeepsAGoodOutcomeWhenTheSourceFails()
    {
        var (cache, vault, _) = Cache();
        vault.Secrets["Tenant-east-ApiKey"] = "k-east";
        await Use(cache, "east");
        Assert.Equal("Missing:Tenant-west-ApiKey", await Use(cache, "west"));

        // An outage is reported but never replaces a working configuration.
        vault.Down = true;
        var issues = await cache.RefreshAsync();
        Assert.Equal(2, issues.Length);
        Assert.All(issues, issue => Assert.Equal(ConfigIssueCategory.SourceFailure, issue.Category));
        Assert.Equal("east:us:k-east", Read(cache, "east"));
        Assert.Equal("SourceFailure:Tenant-west-ApiKey", Read(cache, "west"));

        // A rotation and a newly added secret are both picked up.
        vault.Down = false;
        vault.Secrets["Tenant-east-ApiKey"] = "k-rotated";
        vault.Secrets["Tenant-west-ApiKey"] = "k-west";
        Assert.Empty(await cache.RefreshAsync());
        Assert.Equal("east:us:k-rotated", Read(cache, "east"));
        Assert.Equal("west:us:k-west", Read(cache, "west"));

        // The source ANSWERED that the secret is gone: that is the new outcome.
        vault.Secrets.Remove("Tenant-east-ApiKey");
        Assert.Equal(new[] { "Tenant-east-ApiKey" }, (await cache.RefreshAsync()).Select(issue => issue.Key));
        Assert.Equal("Missing:Tenant-east-ApiKey", Read(cache, "east"));
    }

    [Fact]
    public async Task Set_InstallsAnInstance_WithoutLoading()
    {
        var (cache, vault, _) = Cache();
        cache.Set(new Tenant("east") { Region = "eu", ApiKey = new Secret("k-set") });
        Assert.Equal("east:eu:k-set", Read(cache, "east"));
        Assert.Equal("east:eu:k-set", await Use(cache, "east"));
        Assert.Equal(0, vault.Asked);
    }

    [Fact]
    public async Task ALoadThatThrows_IsNotCached()
    {
        var (cache, vault, _) = Cache();
        vault.Secrets["Tenant-east-ApiKey"] = "k-east";
        var attempts = 0;
        // The second construction is the load's own (the first names the instance).
        Tenant Construct() => ++attempts == 2 ? throw new InvalidOperationException("constructor failed") : new Tenant("east");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cache.GetOrLoadAsync(Construct, tenant => tenant.ApiKey.Reveal(), issue => issue.ToString()));
        Assert.Equal("k-east", await cache.GetOrLoadAsync(Construct, tenant => tenant.ApiKey.Reveal(), issue => issue.ToString()));
    }
}
