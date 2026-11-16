using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Xunit;

using EastFive.Configuration;

namespace EastFive.Api.Tests;

/// <summary>
/// Configuration families keyed by a name (a Ping redirect per tag, SAML per tag): one typed
/// instance per key, member keys with a <c>{key}</c> placeholder, an optional fallback spelling,
/// and the catalog enumerating the instances that configuration actually names.
/// </summary>
public class KeyedConfigurationTests
{
    [ConfigurationType("Widget", Label = "Widget", Description = "Per-tag widget.")]
    private sealed class Widget : IProvideConfiguration, IProvideConfigurationInstanceKey
    {
        public Widget(string tag) => InstanceKey = tag;

        public string InstanceKey { get; }

        [ConfigurationMember("Widget.{key}.Name", FallbackKey = "Legacy.Widget.{key}.Name", Label = "Name", Description = "Display name.")]
        public string Name { get; set; } = default!;

        [ConfigurationMember("Widget.{key}.Size", Kind = ConfigurationMemberKind.Defaulted, Default = "3", Label = "Size", Description = "How big.")]
        public int Size { get; set; }
    }

    private static Func<string, string?> Values(params (string key, string value)[] values)
    {
        var map = values.ToDictionary(pair => pair.key, pair => pair.value);
        return key => map.TryGetValue(key, out var value) ? value : null;
    }

    private static string Load(string tag, Func<string, string?> read)
        => ConfigurationLoader.Load(() => new Widget(tag), read,
            widget => $"{widget.Name}/{widget.Size}",
            (issue, _) => $"missing:{issue.Key}", issue => $"invalid:{issue.Key}", issue => $"failed:{issue.Key}");

    [Fact]
    public void EachInstance_ReadsItsOwnKeys()
    {
        var read = Values(("Widget.east.Name", "East"), ("Widget.west.Name", "West"), ("Widget.west.Size", "9"));
        Assert.Equal("East/3", Load("east", read));
        Assert.Equal("West/9", Load("west", read));
        Assert.Equal("missing:Widget.north.Name", Load("north", read));
    }

    [Fact]
    public void FallbackKey_IsReadOnlyWhenThePrimaryIsAbsent()
    {
        Assert.Equal("Old/3", Load("east", Values(("Legacy.Widget.east.Name", "Old"))));
        Assert.Equal("New/3", Load("east", Values(("Legacy.Widget.east.Name", "Old"), ("Widget.east.Name", "New"))));
    }

    [Fact]
    public void Catalog_EnumeratesTheInstancesConfigurationNames()
    {
        var keys = new[] { "Widget.east.Name", "Widget.west.Size", "Legacy.Widget.south.Name", "Widget.Name", "Other.east.Name" };
        Assert.Equal(new[] { "east", "south", "west" }, ConfigurationCatalog.InstanceKeys(typeof(Widget), keys));
        Assert.True(ConfigurationCatalog.Describe(typeof(Widget)).IsKeyed);
        Assert.Equal("Widget.{key}.Name", ConfigurationCatalog.Describe(typeof(Widget)).Members.First().Key);
    }

    private sealed class KeyedWithoutInstance : IProvideConfiguration
    {
        [ConfigurationMember("Bad.{key}.Name")]
        public string Name { get; set; } = default!;
    }

    [Fact]
    public void APlaceholderWithoutAnInstanceKey_IsADeclarationError()
        => Assert.Throws<InvalidOperationException>(() => ConfigurationMembers.For<KeyedWithoutInstance>());
}

/// <summary>
/// <see cref="Configured.Get{TConfig, TResult}(Func{TConfig, TResult}, Func{ConfigIssue, TResult})"/>:
/// a value-only configuration is loaded at use from the ambient values (so it is always current and
/// needs no startup step); a secret-bearing one is served from the startup load and never loaded here.
/// </summary>
public class ConfiguredTests
{
    [ConfigurationType("ValueOnly", Label = "Value only", Description = "Fixture.")]
    private sealed class ValueOnly : IProvideConfiguration
    {
        [ConfigurationMember("ConfiguredTests.Absent.Value", Kind = ConfigurationMemberKind.Defaulted, Default = "fallback", Label = "Value", Description = "Fixture.")]
        public string Value { get; set; } = default!;
    }

    [ConfigurationType("WithSecret", Label = "With secret", Description = "Fixture.")]
    private sealed class WithSecret : IProvideConfiguration
    {
        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "Key", Description = "Fixture.")]
        public Secret Key { get; set; } = default!;
    }

    [Fact]
    public void ValueOnly_IsLoadedAtUse()
        => Assert.Equal("fallback", Configured.Get<ValueOnly, string>(config => config.Value, issue => issue.ToString()));

    [Fact]
    public void SecretBearing_ComesOnlyFromTheStartupLoad()
    {
        StartupConfiguration.Forget<WithSecret>();
        Assert.Equal(ConfigIssueCategory.NotLoaded, Configured.Get<WithSecret, ConfigIssue?>(_ => null, issue => issue)!.Category);
        StartupConfiguration.Set(new WithSecret { Key = new Secret("k-startup") });
        try
        {
            Assert.Equal("k-startup", Configured.Get<WithSecret, string>(config => config.Key.Reveal(), issue => issue.ToString()));
        }
        finally
        {
            StartupConfiguration.Forget<WithSecret>();
        }
    }

    [Fact]
    public void DeclaredKey_IsReadThroughItsTypedMember_UndeclaredIsNot()
    {
        Assert.True(ConfigurationCatalog.IsDeclaredValueKey("ConfiguredTests.Absent.Value"));
        Assert.False(ConfigurationCatalog.IsDeclaredValueKey("ConfiguredTests.Never.Declared"));
        Assert.Equal("unspecified", Configured.ReadDeclaredValue("ConfiguredTests.Never.Declared", value => value, why => "unspecified"));
    }

    [ConfigurationType("ConfiguredKeyed", Label = "Keyed with secret", Description = "Fixture.")]
    private sealed class KeyedWithSecret : IProvideConfiguration, IProvideConfigurationInstanceKey
    {
        public KeyedWithSecret(string name) => InstanceKey = name;

        public string InstanceKey { get; }

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "Key", Description = "Fixture.")]
        public Secret Key { get; set; } = default!;
    }

    [ConfigurationType("ConfiguredKeyedValue", Label = "Keyed value", Description = "Fixture.")]
    private sealed class KeyedValueOnly : IProvideConfiguration, IProvideConfigurationInstanceKey
    {
        public KeyedValueOnly(string name) => InstanceKey = name;

        public string InstanceKey { get; }

        [ConfigurationMember("ConfiguredTests.{key}.Value", Kind = ConfigurationMemberKind.Defaulted, Default = "fallback", Label = "Value", Description = "Fixture.")]
        public string Value { get; set; } = default!;
    }

    [Fact]
    public async Task KeyedSecretBearing_ComesFromTheInstanceCache_AndNoLongerThrows()
    {
        StartupConfiguration.Forget<KeyedWithSecret>("east");
        Assert.Equal(ConfigIssueCategory.NotLoaded,
            Configured.Get(() => new KeyedWithSecret("east"), _ => (ConfigIssue?)null, issue => issue)!.Category);

        StartupConfiguration.Instances.Set(new KeyedWithSecret("east") { Key = new Secret("k-east") });
        try
        {
            Assert.True(StartupConfiguration.IsLoaded<KeyedWithSecret>("EAST"));
            Assert.Equal("k-east", Configured.Get(() => new KeyedWithSecret("east"), config => config.Key.Reveal(), issue => issue.ToString()));
            Assert.Equal("k-east", StartupConfiguration.Get<KeyedWithSecret, string>("east", config => config.Key.Reveal(), issue => issue.ToString()));
            Assert.Equal("k-east", await Configured.GetAsync(() => new KeyedWithSecret("east"), config => config.Key.Reveal(), issue => issue.ToString()));
            // Another instance of the same type is its own entry.
            Assert.Equal(ConfigIssueCategory.NotLoaded,
                Configured.Get(() => new KeyedWithSecret("west"), _ => (ConfigIssue?)null, issue => issue)!.Category);
        }
        finally
        {
            StartupConfiguration.Forget<KeyedWithSecret>("east");
        }
        Assert.False(StartupConfiguration.IsLoaded<KeyedWithSecret>("east"));
    }

    [Fact]
    public async Task KeyedSecretBearing_GetAsync_LoadsOnFirstUse_ByItsInstancesSecretName()
    {
        // No ConfiguredKeyed-absent-Key anywhere: Missing, or SourceFailure when no test has
        // initialized ambient configuration yet. Either way the instance's own name was read.
        var issue = await Configured.GetAsync(() => new KeyedWithSecret("absent"), _ => (ConfigIssue?)null, issue => issue);
        StartupConfiguration.Forget<KeyedWithSecret>("absent");
        Assert.Equal("ConfiguredKeyed-absent-Key", issue!.Key);
    }

    [Fact]
    public async Task KeyedValueOnly_KeepsItsLiveRead()
    {
        Assert.Equal("fallback", Configured.Get(() => new KeyedValueOnly("east"), config => config.Value, issue => issue.ToString()));
        Assert.Equal("fallback", await Configured.GetAsync(() => new KeyedValueOnly("east"), config => config.Value, issue => issue.ToString()));
        Assert.False(StartupConfiguration.IsLoaded<KeyedValueOnly>("east"));
    }
}

/// <summary>
/// A keyed type may declare SECRET members: with no account member, its instance key is the
/// account of its secrets, so each instance reads <c>{Type}-{InstanceKey}-{Property}</c>. An
/// instance key that cannot name a secret is refused before any secret read, unless the
/// <c>InstanceKey</c> property declares its translation. A declared account member, or an
/// overridden <see cref="IProvideConfiguration.SecretReference"/>, is unchanged.
/// </summary>
public class KeyedSecretConfigurationTests
{
    [ConfigurationType("Hook", Label = "Hook", Description = "Per-id webhook.")]
    [OffersHooks]
    private sealed class Hook : IProvideConfiguration, IProvideConfigurationInstanceKey
    {
        public Hook(string id) => InstanceKey = id;

        public string InstanceKey { get; }

        [ConfigurationMember("Hook.{key}.Url", Kind = ConfigurationMemberKind.Optional, Label = "URL", Description = "Where it posts.")]
        public string? Url { get; set; }

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "Signing secret", Description = "Signs each post.")]
        public Secret SigningSecret { get; set; } = default!;
    }

    [ConfigurationType("EncodedHook", Label = "Encoded hook", Description = "Per-id webhook, any id.")]
    private sealed class EncodedHook : IProvideConfiguration, IProvideConfigurationInstanceKey
    {
        public EncodedHook(string id) => InstanceKey = id;

        [EncodedSecretAccountSegment]
        public string InstanceKey { get; }

        [ConfigurationMember("EncodedHook.{key}.Url", Kind = ConfigurationMemberKind.Optional, Label = "URL", Description = "Where it posts.")]
        public string? Url { get; set; }

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "Signing secret", Description = "Signs each post.")]
        public Secret SigningSecret { get; set; } = default!;
    }

    /// <summary>Keyed AND account-scoped: the account member names the secrets, as it always has.</summary>
    [ConfigurationType("Tenanted", Label = "Tenanted", Description = "Keyed, account-scoped.")]
    private sealed class KeyedWithAccount : IProvideConfiguration, IProvideConfigurationInstanceKey
    {
        public KeyedWithAccount(string tag) => InstanceKey = tag;

        public string InstanceKey { get; }

        [ConfigurationMember("Tenanted.{key}.Account", Account = true, Label = "Account", Description = "The account.")]
        public string Account { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "Key", Description = "The key.")]
        public Secret Key { get; set; } = default!;
    }

    /// <summary>Names its own secrets: nothing about its instance key is judged.</summary>
    [ConfigurationType("Own", Label = "Own", Description = "Names its own secrets.")]
    private sealed class NamesItsOwn : IProvideConfiguration, IProvideConfigurationInstanceKey
    {
        public NamesItsOwn(string tag) => InstanceKey = tag;

        public string InstanceKey { get; }

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "Key", Description = "The key.")]
        public Secret Key { get; set; } = default!;

        SecretReference IProvideConfiguration.SecretReference(string purpose)
            => new($"Legacy-{InstanceKey.Replace('.', '-')}-{purpose}", purpose);
    }

    private sealed class OffersHooksAttribute : Attribute, IProvideConfigurationInstanceLookup
    {
        public Task<TResult> LookupInstancesAsync<TResult>(Type configurationType, ConfigurationDraft draft,
            Func<IReadOnlyList<ConfigurationChoice>, TResult> onChoices,
            Func<string, TResult> onUnavailable)
            => Task.FromResult(draft.ReadValue("Hook.Vendor") is { } vendor
                ? onChoices(new[] { new ConfigurationChoice("wh-1", $"{vendor} orders"), new ConfigurationChoice("wh-2", $"{vendor} refunds") })
                : onUnavailable("Set Hook.Vendor first."));
    }

    private sealed class Recording : ISecretStore
    {
        private readonly ISecretStore inner;
        public readonly List<string> Asked = new();
        public Recording(params (string name, string value)[] secrets)
            => inner = new InMemorySecretStore(secrets.ToDictionary(pair => pair.name, pair => pair.value));
        public IReadOnlyList<SecretSource> Sources => inner.Sources;
        public Task<TResult> GetSecretAsync<TResult>(string name, Func<Secret, SecretSource, TResult> onFound,
            Func<TResult> onMissing, Func<SecretFailure, TResult> onFailure)
        {
            Asked.Add(name);
            return inner.GetSecretAsync(name, onFound, onMissing, onFailure);
        }
    }

    private static Func<string, string?> Values(params (string key, string value)[] values)
    {
        var map = values.ToDictionary(pair => pair.key, pair => pair.value);
        return key => map.TryGetValue(key, out var value) ? value : null;
    }

    private static Task<string> Load<TConfig>(Func<TConfig> construct, Func<TConfig, string> reveal, ISecretStore secrets, Func<string, string?>? read = null)
        where TConfig : IProvideConfiguration
        => ConfigurationLoader.LoadAsync(construct, read ?? Values(), secrets,
            config => $"configured:{reveal(config)}",
            (issue, _) => Task.FromResult($"missing:{issue.Key}"),
            issue => $"invalid:{issue.Key}",
            issue => $"failed:{issue.Key}");

    [Fact]
    public async Task EachInstance_ReadsItsOwnSecret_NamedByItsInstanceKey()
    {
        var store = new Recording(("Hook-east-SigningSecret", "k-east"), ("Hook-west-SigningSecret", "k-west"));
        Assert.Equal("Hook-east-SigningSecret", ((IProvideConfiguration)new Hook("east")).SecretReference("SigningSecret").Name);
        Assert.Equal("configured:k-east", await Load(() => new Hook("east"), hook => hook.SigningSecret.Reveal(), store));
        Assert.Equal("configured:k-west", await Load(() => new Hook("west"), hook => hook.SigningSecret.Reveal(), store));
        Assert.Equal("missing:Hook-north-SigningSecret", await Load(() => new Hook("north"), hook => hook.SigningSecret.Reveal(), store));
        Assert.Equal(new[] { "Hook-east-SigningSecret", "Hook-west-SigningSecret", "Hook-north-SigningSecret" }, store.Asked);
    }

    [Theory]
    [InlineData("wh_abc")]
    [InlineData("has.dots")]
    [InlineData("-lead")]
    [InlineData("")]
    public async Task AnInstanceKeyThatCannotNameASecret_IsInvalid_BeforeAnySecretRead(string instanceKey)
    {
        var store = new Recording(("Hook-wh_abc-SigningSecret", "never"));
        var issue = await ConfigurationLoader.LoadAsync(() => new Hook(instanceKey), Values(), store,
            _ => (ConfigIssue?)null, (missing, _) => Task.FromResult<ConfigIssue?>(missing), invalid => invalid, failed => failed);
        Assert.Equal(ConfigIssueCategory.Invalid, issue!.Category);
        Assert.Equal(nameof(IProvideConfigurationInstanceKey.InstanceKey), issue.Key);
        Assert.Contains("letters, digits and inner dashes", issue.Problem);
        Assert.Empty(store.Asked);
    }

    [Fact]
    public async Task ATranslatedInstanceKey_NamesTheSecret_ValueKeysKeepTheKeyAsWritten()
    {
        var store = new Recording(("EncodedHook-ewh--5fabc-SigningSecret", "k-encoded"));
        var outcome = await Load(() => new EncodedHook("wh_abc"), hook => $"{hook.Url}|{hook.SigningSecret.Reveal()}", store,
            Values(("EncodedHook.wh_abc.Url", "https://hooks.example/abc")));
        Assert.Equal("configured:https://hooks.example/abc|k-encoded", outcome);
        Assert.Equal(new[] { "EncodedHook-ewh--5fabc-SigningSecret" }, store.Asked);
        // An already-valid key is not rewritten, so plain instances keep plain names.
        Assert.Equal("EncodedHook-east-SigningSecret", ((IProvideConfiguration)new EncodedHook("east")).SecretReference("SigningSecret").Name);
    }

    [Fact]
    public async Task ADeclaredAccountMember_StillNamesTheSecrets()
    {
        var store = new Recording(("Tenanted-acme-Key", "k-acme"));
        Assert.Equal("configured:k-acme", await Load(() => new KeyedWithAccount("not.a.segment"), config => config.Key.Reveal(), store,
            Values(("Tenanted.not.a.segment.Account", "acme"))));
        var description = ConfigurationCatalog.Describe(typeof(KeyedWithAccount));
        Assert.False(description.NamesSecretsByInstanceKey);
        Assert.Equal("Tenanted.{key}.Account", description.AccountKey);
        Assert.Equal(new[] { "Tenanted-acme-Key" }, description.SecretNames("acme"));
    }

    [Fact]
    public async Task ATypeThatNamesItsOwnSecrets_IsUnaffected()
    {
        var store = new Recording(("Legacy-a-b-Key", "k-own"));
        Assert.Equal("configured:k-own", await Load(() => new NamesItsOwn("a.b"), config => config.Key.Reveal(), store));
        var description = ConfigurationCatalog.Describe(typeof(NamesItsOwn));
        Assert.False(description.GeneratesSecretNames);
        Assert.False(description.NamesSecretsByInstanceKey);
        Assert.Null(description.AccountProblem("a.b"));
        Assert.Empty(description.SecretNames("a.b"));
    }

    [Fact]
    public async Task AmbientLoad_TakesAConstructor()
    {
        // No Hook-nowhere-SigningSecret anywhere (Missing; SourceFailure when ambient configuration
        // was never initialized): the load read the ambient sources for THIS instance.
        var issue = await ConfigurationLoader.LoadAsync(() => new Hook("nowhere"),
            _ => (ConfigIssue?)null, missing => missing, invalid => invalid, failed => failed);
        Assert.Equal("Hook-nowhere-SigningSecret", issue!.Key);
        Assert.NotEqual(ConfigIssueCategory.Invalid, issue.Category);
    }

    [Fact]
    public void Catalog_NamesAnInstancesSecrets_AndJudgesADraftInstanceKey()
    {
        var hook = ConfigurationCatalog.Describe(typeof(Hook));
        Assert.True(hook.IsKeyed);
        Assert.True(hook.GeneratesSecretNames);
        Assert.True(hook.NamesSecretsByInstanceKey);
        Assert.Null(hook.AccountKey);
        Assert.Null(hook.InstanceKeySegmentTranslation);
        // The existing placeholder: a consumer that matches on it sees a keyed template like any other.
        Assert.Equal("Hook-{account}-SigningSecret", hook.Members.Single(member => member.Kind == ConfigurationMemberKind.Secret).SecretNameTemplate);
        Assert.Equal(new[] { "Hook-{account}-SigningSecret" }, hook.SecretNames());
        Assert.Equal(new[] { "Hook-east-SigningSecret" }, hook.SecretNames("east"));
        Assert.Null(hook.AccountProblem("east"));
        Assert.Contains("instance key", hook.AccountProblem("wh_abc"));
        Assert.NotNull(hook.AccountProblem(""));

        var encoded = ConfigurationCatalog.Describe(typeof(EncodedHook));
        Assert.Equal(nameof(EncodedSecretAccountSegmentAttribute), encoded.InstanceKeySegmentTranslation);
        Assert.Null(encoded.AccountProblem("wh_abc"));
        Assert.Equal(new[] { "EncodedHook-ewh--5fabc-SigningSecret" }, encoded.SecretNames("wh_abc"));
        Assert.NotNull(encoded.AccountProblem(""));
    }

    [Fact]
    public async Task Inspect_ReportsAKeyedInstancesSecret_ByItsRealName()
    {
        var store = new Recording(("Hook-east-SigningSecret", "k-east"));
        var rows = await ConfigurationLoader.InspectAsync(() => new Hook("east"), Values(), store);
        Assert.Equal(new[] { "Hook.east.Url", "Hook-east-SigningSecret" }, rows.Select(row => row.Name));
        Assert.Equal(new[] { ConfigurationMemberPresence.Absent, ConfigurationMemberPresence.Present }, rows.Select(row => row.Presence));

        var unnamed = await ConfigurationLoader.InspectAsync(() => new Hook("wh_abc"), Values(), store);
        Assert.Equal(ConfigurationMemberPresence.Unnamed, unnamed[1].Presence);
        Assert.Equal(new[] { "Hook-east-SigningSecret" }, store.Asked);
    }

    [Fact]
    public async Task ATypeOffersItsInstances_ThroughATypeAttribute()
    {
        var hook = ConfigurationCatalog.Describe(typeof(Hook));
        Assert.True(hook.HasInstanceLookup);
        Assert.False(ConfigurationCatalog.Describe(typeof(EncodedHook)).HasInstanceLookup);
        Assert.Null(ConfigurationCatalog.Describe(typeof(EncodedHook)).InstanceLookup);

        var offered = await hook.InstanceLookup!.LookupInstancesAsync(typeof(Hook),
            ConfigurationDraft.From(new Dictionary<string, string> { ["Hook.Vendor"] = "Acme" }, new Dictionary<string, string>()),
            choices => string.Join(",", choices.Select(choice => $"{choice.Value}={choice.Label}")),
            why => why);
        Assert.Equal("wh-1=Acme orders,wh-2=Acme refunds", offered);

        var unavailable = await hook.InstanceLookup!.LookupInstancesAsync(typeof(Hook),
            ConfigurationDraft.From(new Dictionary<string, string>(), new Dictionary<string, string>()),
            choices => "choices", why => why);
        Assert.Equal("Set Hook.Vendor first.", unavailable);
    }

    private sealed class DropDotsAttribute : Attribute, IProvideSecretAccountSegment
    {
        public string SecretAccountSegment(string account) => account;
    }

    [ConfigurationType("TwiceKeyed")]
    private sealed class TwoTranslationsOnTheInstanceKey : IProvideConfiguration, IProvideConfigurationInstanceKey
    {
        [EncodedSecretAccountSegment]
        [DropDots]
        public string InstanceKey { get; } = "x";

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        public Secret Key { get; set; } = default!;
    }

    [ConfigurationType("Stray")]
    private sealed class TranslationOnAnUndeclaredProperty : IProvideConfiguration, IProvideConfigurationInstanceKey
    {
        public string InstanceKey { get; } = "x";

        [EncodedSecretAccountSegment]
        public string Derived => InstanceKey;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        public Secret Key { get; set; } = default!;
    }

    [ConfigurationType("NotKeyed")]
    private sealed class TranslationOnAnInstanceKeyThatIsNotOne : IProvideConfiguration
    {
        [EncodedSecretAccountSegment]
        public string InstanceKey { get; } = "x";

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        public Secret Key { get; set; } = default!;
    }

    [ConfigurationType("Both")]
    private sealed class TranslationOnTheInstanceKeyOfAnAccountScopedType : IProvideConfiguration, IProvideConfigurationInstanceKey
    {
        [EncodedSecretAccountSegment]
        public string InstanceKey { get; } = "x";

        [ConfigurationMember("Both.{key}.Account", Account = true)]
        public string Account { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        public Secret Key { get; set; } = default!;
    }

    [OffersHooks]
    private sealed class LookupOnATypeThatIsNotKeyed : IProvideConfiguration
    {
        [ConfigurationMember("Unkeyed.Value")]
        public string Value { get; set; } = default!;
    }

    [Theory]
    [InlineData(typeof(TwoTranslationsOnTheInstanceKey))]
    [InlineData(typeof(TranslationOnAnUndeclaredProperty))]
    [InlineData(typeof(TranslationOnAnInstanceKeyThatIsNotOne))]
    [InlineData(typeof(TranslationOnTheInstanceKeyOfAnAccountScopedType))]
    [InlineData(typeof(LookupOnATypeThatIsNotKeyed))]
    public void AMisplacedDeclaration_IsADeclarationError(Type type)
        => Assert.Throws<InvalidOperationException>(() => ConfigurationMembers.For(type));
}
