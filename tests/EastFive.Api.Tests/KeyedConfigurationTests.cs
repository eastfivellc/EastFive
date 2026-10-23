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
}
