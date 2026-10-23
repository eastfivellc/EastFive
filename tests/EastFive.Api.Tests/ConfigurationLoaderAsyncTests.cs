using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

using EastFive.Configuration;

namespace EastFive.Api.Tests;

/// <summary>
/// The asynchronous loader: values from the values reader, secrets ONLY from the secret store by
/// generated name, store failures as source failures, the sync path refusing secret types, a
/// type resolving its own member, the doctor's inspection, and startup-loaded configuration.
/// </summary>
public class ConfigurationLoaderAsyncTests
{
    [ConfigurationType("Mail")]
    private sealed class Mail : IProvideConfiguration
    {
        [ConfigurationMember("Mail.Account", Account = true)]
        public string Account { get; set; } = default!;

        [ConfigurationMember("Mail.Sender")]
        public string Sender { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        public Secret ApiKey { get; set; } = default!;

        [ConfigurationMember("Mail.Retries", Kind = ConfigurationMemberKind.Defaulted, Default = "2")]
        public int Retries { get; set; }
    }

    [ConfigurationType("Local")]
    private sealed class SelfResolving : IProvideConfiguration, IResolveConfigurationMember
    {
        [ConfigurationMember("Local.Account", Account = true)]
        public string Account { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        public Secret Key { get; set; } = default!;

        public TResult ResolveMember<TResult>(ConfigurationMember member, Func<string, string?> readValue,
            Func<string, string, TResult> onResolved, Func<TResult> onNotResolved, Func<ConfigIssue, TResult> onInvalid)
            => member.IsSecret && Account == "loopback" ? onResolved("well-known", "fabricated") : onNotResolved();
    }

    private sealed class Recording : ISecretStore
    {
        private readonly ISecretStore inner;
        public readonly List<string> Asked = new();
        public Recording(ISecretStore inner) => this.inner = inner;
        public IReadOnlyList<SecretSource> Sources => inner.Sources;
        public Task<TResult> GetSecretAsync<TResult>(string name, Func<Secret, SecretSource, TResult> onFound,
            Func<TResult> onMissing, Func<string, TResult> onFailure)
        {
            Asked.Add(name);
            return inner.GetSecretAsync(name, onFound, onMissing, onFailure);
        }
    }

    private sealed class Down : ISecretStore
    {
        public IReadOnlyList<SecretSource> Sources => new[] { new SecretSource(SecretSource.Vault, "down") };
        public Task<TResult> GetSecretAsync<TResult>(string name, Func<Secret, SecretSource, TResult> onFound,
            Func<TResult> onMissing, Func<string, TResult> onFailure)
            => Task.FromResult(onFailure("Vault down answered 503."));
    }

    private static readonly Dictionary<string, string> Values = new()
    {
        ["Mail.Account"] = "acme",
        ["Mail.Sender"] = "noreply@acme.example",
        // A secret in the VALUES store under its own name must never be used.
        ["Mail-acme-ApiKey"] = "k-values-must-not-win",
    };

    private static Func<string, string?> Read(IReadOnlyDictionary<string, string> values) => key => values.GetValueOrDefault(key);

    private static Task<string> LoadMail(ISecretStore secrets, IReadOnlyDictionary<string, string>? values = null)
        => ConfigurationLoader.LoadAsync<Mail, string>(Read(values ?? Values), secrets,
            mail => $"configured:{mail.Sender}:{mail.ApiKey.Reveal()}:{mail.Retries}",
            issue => $"missing:{issue.Key}",
            issue => $"invalid:{issue.Key}",
            issue => $"source:{issue.Key}");

    [Fact]
    public async Task Secrets_ComeFromTheStore_ByGeneratedName_NeverFromValues()
    {
        var store = new Recording(new InMemorySecretStore(new Dictionary<string, string> { ["Mail-acme-ApiKey"] = "k-store" }));
        Assert.Equal("configured:noreply@acme.example:k-store:2", await LoadMail(store));
        Assert.Equal(new[] { "Mail-acme-ApiKey" }, store.Asked);

        Assert.Equal("missing:Mail-acme-ApiKey", await LoadMail(new InMemorySecretStore(new Dictionary<string, string>())));
    }

    [Fact]
    public async Task StoreFailure_IsSourceFailure_NeverMissing()
        => Assert.Equal("source:Mail-acme-ApiKey", await LoadMail(new Down()));

    [Fact]
    public async Task MissingAccount_StopsBeforeAnySecretRead()
    {
        var store = new Recording(new InMemorySecretStore(new Dictionary<string, string>()));
        var values = new Dictionary<string, string>(Values);
        values.Remove("Mail.Account");
        Assert.Equal("missing:Mail.Account", await LoadMail(store, values));
        values["Mail.Account"] = "acme.prod";
        Assert.Equal("invalid:Mail.Account", await LoadMail(store, values));
        Assert.Empty(store.Asked);
    }

    [Fact]
    public async Task SuppliedSecret_Resumes()
    {
        var outcome = await ConfigurationLoader.LoadAsync(() => new Mail(), Read(Values), new InMemorySecretStore(new Dictionary<string, string>()),
            mail => $"configured:{mail.ApiKey.Reveal()}",
            (issue, supply) => issue.Key == "Mail-acme-ApiKey" ? supply("k-supplied") : Task.FromResult("unexpected"),
            issue => $"invalid:{issue.Key}",
            issue => $"source:{issue.Key}");
        Assert.Equal("configured:k-supplied", outcome);
    }

    [Fact]
    public void SyncLoad_RefusesTypesWithSecrets()
        => Assert.Throws<InvalidOperationException>(() => ConfigurationLoader.Load<Mail, string>(Read(Values),
            _ => "configured", (_, _) => "missing", _ => "invalid", _ => "source"));

    [Fact]
    public async Task TypeResolvesItsOwnMember_WithoutAskingTheStore()
    {
        var store = new Recording(new InMemorySecretStore(new Dictionary<string, string> { ["Local-loopback-Key"] = "from-store" }));
        var outcome = await ConfigurationLoader.LoadAsync<SelfResolving, string>(Read(new Dictionary<string, string> { ["Local.Account"] = "loopback" }), store,
            config => config.Key.Reveal(), issue => $"missing:{issue.Key}", issue => $"invalid:{issue.Key}", issue => $"source:{issue.Key}");
        Assert.Equal("well-known", outcome);
        Assert.Empty(store.Asked);

        var status = await ConfigurationLoader.InspectAsync<SelfResolving>(Read(new Dictionary<string, string> { ["Local.Account"] = "loopback" }), store);
        Assert.Equal("fabricated", status.Single(row => row.Member.IsSecret).Source);
    }

    [Fact]
    public async Task Inspect_NamesEveryMember_TheSecretsSource_AndNoValues()
    {
        var store = new CompositeSecretStore(
            new ConfigurationSecretOverrides(Read(new Dictionary<string, string> { ["Secrets:Mail-acme-ApiKey"] = "k-override" })),
            new Down());
        var rows = await ConfigurationLoader.InspectAsync<Mail>(Read(Values), store);
        Assert.Equal(new[] { "Mail.Account", "Mail.Sender", "Mail-acme-ApiKey", "Mail.Retries" }, rows.Select(row => row.Name));
        Assert.Equal(new[] { ConfigurationMemberPresence.Present, ConfigurationMemberPresence.Present, ConfigurationMemberPresence.Present, ConfigurationMemberPresence.Defaulted },
            rows.Select(row => row.Presence));
        Assert.Equal("local override (Secrets)", rows[2].Source);
        Assert.DoesNotContain(rows, row => row.ToString().Contains("k-override"));

        var down = await ConfigurationLoader.InspectAsync<Mail>(Read(Values), new Down());
        Assert.Equal(ConfigurationMemberPresence.Failed, down[2].Presence);
        Assert.Equal(new[] { "Mail-acme-ApiKey" }, await ConfigurationLoader.MissingAsync<Mail>(Read(Values), new Down()));
        Assert.Equal(new[] { "Mail.Account", "Mail.Sender" },
            await ConfigurationLoader.MissingAsync<Mail>(Read(new Dictionary<string, string>()), store));
        var invalidAccount = await ConfigurationLoader.InspectAsync<Mail>(Read(new Dictionary<string, string> { ["Mail.Account"] = "acme.prod" }), store);
        Assert.Equal(ConfigurationMemberPresence.Invalid, invalidAccount[0].Presence);
        Assert.Equal(ConfigurationMemberPresence.Unnamed, invalidAccount[2].Presence);
    }

    [ConfigurationType("Boot")]
    private sealed class Boot : IProvideConfiguration
    {
        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        public Secret Signing { get; set; } = default!;
    }

    [Fact]
    public async Task StartupConfiguration_IsLoadedOnce_AndNeverLoadsOnRead()
    {
        StartupConfiguration.Forget<Boot>();
        Assert.Equal(ConfigIssueCategory.NotLoaded,
            StartupConfiguration.Get<Boot, ConfigIssue?>(_ => null, issue => issue)!.Category);

        StartupConfiguration.Set(new Boot { Signing = new Secret("k-boot") });
        Assert.Equal("k-boot", StartupConfiguration.Get<Boot, string>(boot => boot.Signing.Reveal(), issue => issue.ToString()));

        // Loaded from the ambient store (no Boot-Signing anywhere; Missing, or SourceFailure if no
        // test has initialized ambient configuration yet): the outcome is cached, reads never reload.
        StartupConfiguration.Forget<Boot>();
        var issue = await StartupConfiguration.LoadAsync<Boot>();
        Assert.Equal("Boot-Signing", issue!.Key);
        Assert.Equal(issue, StartupConfiguration.Get<Boot, ConfigIssue?>(_ => null, problem => problem));
        StartupConfiguration.Forget<Boot>();
    }
}

/// <summary>
/// A host or test harness that blocks on startup loading from a thread with a synchronization
/// context (xUnit's, a UI thread) must not deadlock: the loader never resumes on the caller's
/// context.
/// </summary>
public class ConfigurationLoaderContextTests
{
    /// <summary>A context whose posted continuations are never run — the deadlock a blocking
    /// caller produces when every worker of its context is waiting.</summary>
    private sealed class NeverPumped : SynchronizationContext
    {
        public int Posted;
        public override void Post(SendOrPostCallback d, object? state) => Interlocked.Increment(ref Posted);
    }

    private sealed class Slow : ISecretStore
    {
        public IReadOnlyList<SecretSource> Sources => new[] { new SecretSource(SecretSource.Vault, "slow") };
        public async Task<TResult> GetSecretAsync<TResult>(string name, Func<Secret, SecretSource, TResult> onFound,
            Func<TResult> onMissing, Func<string, TResult> onFailure)
        {
            await Task.Delay(20).ConfigureAwait(false);
            return onFound(new Secret("k-slow"), Sources[0]);
        }
    }

    [ConfigurationType("Ctx")]
    private sealed class Ctx : IProvideConfiguration
    {
        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        public Secret Key { get; set; } = default!;
    }

    [Fact]
    public void BlockingOnLoadAsync_FromAContextThread_DoesNotDeadlock()
    {
        string? outcome = null;
        var context = new NeverPumped();
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(context);
            outcome = ConfigurationLoader.LoadAsync<Ctx, string>(_ => null, new Slow(),
                ctx => ctx.Key.Reveal(), issue => "missing", issue => "invalid", issue => "failed").GetAwaiter().GetResult();
        });
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "LoadAsync resumed on the caller's context and deadlocked");
        Assert.Equal("k-slow", outcome);
        Assert.Equal(0, context.Posted);
    }
}
