using System;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;
using Xunit;

using EastFive.Configuration;

namespace EastFive.Api.Tests;

/// <summary>
/// The typed-configuration loader's outcome semantics, exercised with in-memory read sources
/// and fixture configuration types (no driver, no ambient configuration, no storage):
/// missing vs invalid vs source-failure, caller-mitigated missing values, declaration-order
/// resumption, defaults, partial modes, and the no-values guarantee of every issue string.
/// </summary>
public class ConfigurationLoaderTests
{
    private sealed class Probe : IProvideConfiguration
    {
        [ConfigurationMember("Probe.Name")]
        public string Name { get; init; } = default!;

        [ConfigurationMember("Probe.Endpoint")]
        public Uri Endpoint { get; init; } = default!;

        [ConfigurationMember("Probe.Label", Kind = ConfigurationMemberKind.Optional)]
        public string? Label { get; init; }

        [ConfigurationMember("Probe.Retries", Kind = ConfigurationMemberKind.Defaulted, Default = "3")]
        public int Retries { get; init; }

        [ConfigurationMember("Probe.Token", Kind = ConfigurationMemberKind.Secret, Purpose = "api")]
        public Secret Token { get; init; } = default!;

        public string? Validate() => Retries is < 0 or > 10 ? "Probe.Retries" : null;
    }

    private sealed class Loose : IProvideConfiguration
    {
        [ConfigurationMember("Loose.Only", Kind = ConfigurationMemberKind.Optional)]
        public string? Only { get; init; }

        /// <summary>Derived, undeclared: a configuration type may expose computed members.</summary>
        public string OnlyOrDefault => Only ?? "(default)";
    }

    /// <summary>A secret whose reference is derived from an earlier member (account -> secret name).</summary>
    private sealed class AccountKeyed : IProvideConfiguration
    {
        [ConfigurationMember("Keyed.Account", Kind = ConfigurationMemberKind.Optional)]
        public string? Account { get; set; }

        [ConfigurationMember("Keyed.Key", Kind = ConfigurationMemberKind.Secret, Purpose = "api")]
        public Secret Key { get; set; } = default!;

        public SecretReference SecretReference(string purpose)
            => new(string.IsNullOrWhiteSpace(Account) ? "Keyed.Key" : $"Keyed.{Account}.Key", purpose);
    }

    private static Dictionary<string, string> Complete() => new(StringComparer.Ordinal)
    {
        ["Probe.Name"] = "  probe-one ",
        ["Probe.Endpoint"] = "https://probe.example/api",
        ["Probe.Label"] = "friendly",
        ["Probe.Retries"] = "5",
        ["Probe.Token"] = "tok-very-secret",
    };

    private static Func<string, string?> Source(IReadOnlyDictionary<string, string> values, List<string>? reads = null)
        => key =>
        {
            reads?.Add(key);
            return values.TryGetValue(key, out var value) ? value : null;
        };

    private static string Load<TConfig>(Func<string, string?> readSetting, Action<TConfig>? inspect = null)
        where TConfig : IProvideConfiguration, new()
        => ConfigurationLoader.Load<TConfig, string>(readSetting,
            onConfigured: config => { inspect?.Invoke(config); return "configured"; },
            onMissing: (issue, _) => $"missing:{issue.Key}",
            onInvalid: issue => $"invalid:{issue.Key}",
            onSourceFailure: issue => $"source:{issue.Key}");

    [Fact]
    public void Configured_ConvertsTrimsAndReadsEveryDeclaredKeyOnce()
    {
        var reads = new List<string>();
        var outcome = Load<Probe>(Source(Complete(), reads), probe =>
        {
            Assert.Equal("probe-one", probe.Name);
            Assert.Equal(new Uri("https://probe.example/api"), probe.Endpoint);
            Assert.Equal("friendly", probe.Label);
            Assert.Equal(5, probe.Retries);
            Assert.Equal("tok-very-secret", probe.Token.Reveal());
        });
        Assert.Equal("configured", outcome);
        Assert.Equal(ConfigurationMembers.Keys<Probe>(), reads);
    }

    [Fact]
    public void OptionalAbsent_IsPartialConfiguration_NotAnIssue()
    {
        var values = Complete();
        values.Remove("Probe.Label");
        Assert.Equal("configured", Load<Probe>(Source(values), probe => Assert.Null(probe.Label)));
    }

    [Fact]
    public void DefaultedAbsent_AppliesTheDefault_AndStillValidates()
    {
        var values = Complete();
        values.Remove("Probe.Retries");
        Assert.Equal("configured", Load<Probe>(Source(values), probe => Assert.Equal(3, probe.Retries)));

        values["Probe.Retries"] = "50"; // present, converts, fails Validate()
        Assert.Equal("invalid:Probe.Retries", Load<Probe>(Source(values)));
    }

    [Fact]
    public void InvalidSourceValue_IsInvalid_NotMissing()
    {
        var values = Complete();
        values["Probe.Endpoint"] = "not a url";
        Assert.Equal("invalid:Probe.Endpoint", Load<Probe>(Source(values)));

        values["Probe.Endpoint"] = "https://ok.example";
        values["Probe.Retries"] = "three";
        Assert.Equal("invalid:Probe.Retries", Load<Probe>(Source(values)));
    }

    [Fact]
    public void RequiredMissing_CallerReturns_NothingContinues()
    {
        var values = Complete();
        values.Remove("Probe.Name");
        var configured = false;
        var outcome = ConfigurationLoader.Load<Probe, string>(Source(values),
            onConfigured: _ => { configured = true; return "configured"; },
            onMissing: (issue, _) =>
            {
                Assert.Equal(typeof(Probe), issue.ConfigurationType);
                Assert.Equal(ConfigurationMemberKind.Required, issue.Kind);
                Assert.Equal(ConfigIssueCategory.Missing, issue.Category);
                return "caller-declined";
            },
            onInvalid: issue => $"invalid:{issue.Key}",
            onSourceFailure: issue => $"source:{issue.Key}");
        Assert.Equal("caller-declined", outcome);
        Assert.False(configured);
    }

    [Fact]
    public void RequiredMissing_CallerSupplies_ResumesToConfigured()
    {
        var values = Complete();
        values.Remove("Probe.Name");
        var outcome = ConfigurationLoader.Load<Probe, string>(Source(values),
            onConfigured: probe => $"configured:{probe.Name}",
            onMissing: (issue, supply) => supply(" supplied-name "),
            onInvalid: issue => $"invalid:{issue.Key}",
            onSourceFailure: issue => $"source:{issue.Key}");
        Assert.Equal("configured:supplied-name", outcome);
    }

    [Fact]
    public void RequiredMissing_CallerSuppliesInvalid_IsInvalid()
    {
        var values = Complete();
        values.Remove("Probe.Endpoint");
        var outcome = ConfigurationLoader.Load<Probe, string>(Source(values),
            onConfigured: _ => "configured",
            onMissing: (issue, supply) => supply("still not a url"),
            onInvalid: issue => $"invalid:{issue.Key}:{issue.Category}",
            onSourceFailure: issue => $"source:{issue.Key}");
        Assert.Equal("invalid:Probe.Endpoint:Invalid", outcome);

        values.Remove("Probe.Name");
        var blank = ConfigurationLoader.Load<Probe, string>(Source(values),
            onConfigured: _ => "configured",
            onMissing: (issue, supply) => supply("   "),
            onInvalid: issue => $"invalid:{issue.Key}",
            onSourceFailure: issue => $"source:{issue.Key}");
        Assert.Equal("invalid:Probe.Name", blank);
    }

    [Fact]
    public void TwoMissing_ReachOnMissingTwice_InDeclarationOrder()
    {
        var values = Complete();
        values.Remove("Probe.Endpoint");
        values.Remove("Probe.Token");
        var asked = new List<string>();
        var outcome = ConfigurationLoader.Load<Probe, string>(Source(values),
            onConfigured: probe => $"configured:{probe.Endpoint}:{probe.Token.Reveal()}",
            onMissing: (issue, supply) =>
            {
                asked.Add(issue.Key);
                return supply(issue.Key == "Probe.Endpoint" ? "https://supplied.example/" : "supplied-token");
            },
            onInvalid: issue => $"invalid:{issue.Key}",
            onSourceFailure: issue => $"source:{issue.Key}");
        Assert.Equal(new[] { "Probe.Endpoint", "Probe.Token" }, asked);
        Assert.Equal("configured:https://supplied.example/:supplied-token", outcome);
    }

    [Fact]
    public void SecretMissing_IsMissingWithSecretKind_AndReadsTheReferenceKey()
    {
        var values = Complete();
        values.Remove("Probe.Token");
        var reads = new List<string>();
        var outcome = ConfigurationLoader.Load<Probe, string>(Source(values, reads),
            onConfigured: _ => "configured",
            onMissing: (issue, _) => $"missing:{issue.Key}:{issue.Kind}",
            onInvalid: issue => $"invalid:{issue.Key}",
            onSourceFailure: issue => $"source:{issue.Key}");
        Assert.Equal("missing:Probe.Token:Secret", outcome);
        Assert.Contains(((IProvideConfiguration)new Probe()).SecretReference("api").Key, reads);
    }

    [Fact]
    public void SourceThrows_IsSourceFailure_NeverMissing()
    {
        var outcome = Load<Probe>(key => key == "Probe.Endpoint"
            ? throw new UnauthorizedAccessException("vault said no: tok-very-secret")
            : Complete()[key]);
        Assert.Equal("source:Probe.Endpoint", outcome);
    }

    [Fact]
    public void EmptyDictionary_NeverLightsUpFromAmbientConfiguration()
    {
        // The IDescribeLibrary probe constraint: a describer handed an empty dictionary must
        // report missing, whatever the process's global configuration happens to hold.
        var reads = new List<string>();
        Assert.Equal("missing:Probe.Name", Load<Probe>(Source(new Dictionary<string, string>(), reads)));
        Assert.Equal(new[] { "Probe.Name" }, reads);
    }

    [Fact]
    public void IssuesAndSecretsCarryNoValues()
    {
        var values = Complete();
        values["Probe.Endpoint"] = "not a url tok-very-secret";
        ConfigIssue? captured = null;
        ConfigurationLoader.Load<Probe, string>(Source(values),
            onConfigured: _ => "configured",
            onMissing: (issue, _) => "missing",
            onInvalid: issue => { captured = issue; return "invalid"; },
            onSourceFailure: issue => "source");
        Assert.NotNull(captured);
        Assert.DoesNotContain("tok-very-secret", captured!.ToString());
        Assert.DoesNotContain("not a url", captured.ToString());
        Assert.True(captured.ToString().All(c => c < 128), "issue text must stay ASCII for response headers");

        var secret = new Secret("tok-very-secret");
        Assert.Equal("[secret]", secret.ToString());
        Assert.DoesNotContain("tok-very-secret", $"{secret}");
        Assert.DoesNotContain("tok-very-secret", JsonConvert.SerializeObject(secret));
        Assert.DoesNotContain("tok-very-secret", System.Text.Json.JsonSerializer.Serialize(secret));

        string? json = null;
        Load<Probe>(Source(Complete()), probe => json = JsonConvert.SerializeObject(probe));
        Assert.NotNull(json);
        Assert.DoesNotContain("tok-very-secret", json);
        Assert.Contains("probe-one", json); // non-secret members still serialize
        Assert.Throws<ArgumentException>(() => new Secret("  "));
    }

    [Fact]
    public void Missing_ListsEveryRequiredOrSecretGap_InDeclarationOrder()
    {
        Assert.Equal(new[] { "Probe.Name", "Probe.Endpoint", "Probe.Token" },
            ConfigurationLoader.Missing<Probe>(Source(new Dictionary<string, string>())));
        var values = Complete();
        values.Remove("Probe.Label");   // Optional: never listed
        values.Remove("Probe.Retries"); // Defaulted: never listed
        values["Probe.Token"] = "  ";
        Assert.Equal(new[] { "Probe.Token" }, ConfigurationLoader.Missing<Probe>(Source(values)));
        Assert.Empty(ConfigurationLoader.Missing<Probe>(Source(Complete())));
    }

    [Fact]
    public void ConfigurationWithoutLoadDriver_LoadsFine()
    {
        Assert.Equal("configured", Load<Loose>(Source(new Dictionary<string, string>()), loose => Assert.Equal("(default)", loose.OnlyOrDefault)));
        Assert.Equal("configured", Load<Loose>(Source(new Dictionary<string, string> { ["Loose.Only"] = "x" }), loose => Assert.Equal("x", loose.Only)));
        Assert.Null(((IProvideConfiguration)new Loose()).Validate());
        Assert.Equal(new[] { "Loose.Only" }, ConfigurationMembers.Keys<Loose>());
    }

    [Fact]
    public void SecretReferenceDerivedFromAnEarlierMember_IsHonoredByLoadAndByTheChecklist()
    {
        var values = new Dictionary<string, string> { ["Keyed.Account"] = "acme", ["Keyed.acme.Key"] = "k-acme", ["Keyed.Key"] = "k-global" };
        Assert.Equal("configured", Load<AccountKeyed>(Source(values), keyed => Assert.Equal("k-acme", keyed.Key.Reveal())));
        Assert.Equal("configured", Load<AccountKeyed>(Source(new Dictionary<string, string> { ["Keyed.Key"] = "k-global" }), keyed => Assert.Equal("k-global", keyed.Key.Reveal())));
        Assert.Equal(new[] { "Keyed.acme.Key" }, ConfigurationLoader.Missing<AccountKeyed>(Source(new Dictionary<string, string> { ["Keyed.Account"] = "acme", ["Keyed.Key"] = "k-global" })));
        Assert.Equal(new[] { "Keyed.Key" }, ConfigurationLoader.Missing<AccountKeyed>(Source(new Dictionary<string, string>())));
    }

    [Fact]
    public void KeysDeriveFromTheDeclaration()
    {
        Assert.Equal(new[] { "Probe.Name", "Probe.Endpoint", "Probe.Label", "Probe.Retries", "Probe.Token" }, ConfigurationMembers.Keys<Probe>());
        Assert.Equal(new[] { "Probe.Name", "Probe.Endpoint", "Probe.Label", "Probe.Retries" }, ConfigurationMembers.PublicKeys<Probe>());
        Assert.Equal(new[] { "Probe.Token" }, ConfigurationMembers.SecretKeys<Probe>());
        Assert.Equal(new SecretReference("Probe.Token", "api"), ((IProvideConfiguration)new Probe()).SecretReference("api"));
        Assert.Throws<InvalidOperationException>(() => ((IProvideConfiguration)new Probe()).SecretReference("nope"));
    }

    [Fact]
    public void ConstructorInputs_SelectTheSecret()
    {
        var reads = new List<string>();
        var outcome = ConfigurationLoader.Load(() => new Tenanted("acme"),
            Source(new Dictionary<string, string> { ["Tenanted.acme.Token"] = "acme-token" }, reads),
            onConfigured: config => config.Token.Reveal(),
            onMissing: (issue, _) => $"missing:{issue.Key}",
            onInvalid: issue => $"invalid:{issue.Key}",
            onSourceFailure: issue => $"source:{issue.Key}");
        Assert.Equal("acme-token", outcome);
        Assert.Equal(new[] { "Tenanted.acme.Token" }, reads);
    }

    private sealed class Tenanted : IProvideConfiguration
    {
        private readonly string tenant;
        public Tenanted(string tenant) => this.tenant = tenant;

        [ConfigurationMember("Tenanted.Token", Kind = ConfigurationMemberKind.Secret, Purpose = "api")]
        public Secret Token { get; init; } = default!;

        public SecretReference SecretReference(string purpose) => new($"Tenanted.{tenant}.Token", purpose);
    }

    private sealed class SecretTypedAsString : IProvideConfiguration
    {
        [ConfigurationMember("Bad.Token", Kind = ConfigurationMemberKind.Secret, Purpose = "api")]
        public string Token { get; init; } = default!;
    }

    private sealed class DefaultedSecret : IProvideConfiguration
    {
        [ConfigurationMember("Bad.Token", Kind = ConfigurationMemberKind.Secret, Purpose = "api", Default = "x")]
        public Secret Token { get; init; } = default!;
    }

    private sealed class OptionalNonNullable : IProvideConfiguration
    {
        [ConfigurationMember("Bad.Count", Kind = ConfigurationMemberKind.Optional)]
        public int Count { get; init; }
    }

    [Fact]
    public void MalformedDeclarations_ThrowAtReflection_NotAsConfigIssues()
    {
        Assert.Throws<InvalidOperationException>(() => ConfigurationMembers.For<SecretTypedAsString>());
        Assert.Throws<InvalidOperationException>(() => ConfigurationMembers.For<DefaultedSecret>());
        Assert.Throws<InvalidOperationException>(() => ConfigurationMembers.For<OptionalNonNullable>());
    }
}
