using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Xunit;

using EastFive.Configuration;

namespace EastFive.Api.Tests;

/// <summary>
/// An account member may declare how its value becomes a secret-name segment
/// (<see cref="IProvideSecretAccountSegment"/>): an exact, type-specific translation, so an
/// account value that is not itself a valid segment (a Google client id, with its dots) still
/// names one secret — while a value the translation cannot make valid is still refused, never
/// blindly mapped.
/// </summary>
public class SecretAccountSegmentTests
{
    /// <summary>Drops a fixed suffix, the way a vendor's ids carry a constant domain.</summary>
    private sealed class DropSuffixAttribute : Attribute, IProvideSecretAccountSegment
    {
        public string SecretAccountSegment(string account)
            => account.EndsWith(".vendor.example", StringComparison.OrdinalIgnoreCase)
                ? account[..^".vendor.example".Length]
                : account;
    }

    [ConfigurationType("Vendor")]
    private sealed class Vendor : IProvideConfiguration
    {
        [ConfigurationMember("Vendor.ClientId", Account = true)]
        [DropSuffix]
        public string ClientId { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        public Secret ClientSecret { get; set; } = default!;
    }

    private static Func<string, string?> Values(string clientId)
        => key => key == "Vendor.ClientId" ? clientId : null;

    [Fact]
    public void TranslatedAccount_NamesTheSecret_TheMemberKeepsItsValue()
    {
        var vendor = new Vendor { ClientId = "1234-abc.vendor.example" };
        Assert.Equal("Vendor-1234-abc-ClientSecret", ((IProvideConfiguration)vendor).SecretReference("ClientSecret").Name);
        Assert.False(ConfigurationMembers.For<Vendor>().HasInvalidAccount(vendor));
        Assert.Equal("1234-abc.vendor.example", vendor.ClientId);
    }

    [Fact]
    public async Task Load_ReadsTheTranslatedName()
    {
        var secrets = new InMemorySecretStore(new Dictionary<string, string> { ["Vendor-1234-abc-ClientSecret"] = "k-vendor" });
        var outcome = await ConfigurationLoader.LoadAsync<Vendor, string>(Values("1234-abc.vendor.example"), secrets,
            vendor => $"{vendor.ClientId}|{vendor.ClientSecret.Reveal()}",
            issue => $"missing:{issue.Key}", issue => $"invalid:{issue.Key}", issue => $"failed:{issue.Key}");
        Assert.Equal("1234-abc.vendor.example|k-vendor", outcome);
        Assert.Equal(new[] { "Vendor-1234-abc-ClientSecret" },
            await ConfigurationLoader.MissingAsync<Vendor>(Values("1234-abc.vendor.example"), new InMemorySecretStore(new Dictionary<string, string>())));
    }

    [Fact]
    public async Task UntranslatableAccount_IsStillRefused_NeverMapped()
    {
        var outcome = await ConfigurationLoader.LoadAsync<Vendor, string>(Values("1234.abc.other.example"),
            new InMemorySecretStore(new Dictionary<string, string>()),
            _ => "configured", issue => $"missing:{issue.Key}", issue => $"invalid:{issue.Key}", issue => $"failed:{issue.Key}");
        Assert.Equal("invalid:Vendor.ClientId", outcome);
    }

    [Fact]
    public void Catalog_TranslatesTheAccountItIsGiven()
    {
        var description = ConfigurationCatalog.Describe(typeof(Vendor));
        Assert.Equal(new[] { "Vendor-{account}-ClientSecret" }, description.SecretNames());
        Assert.Equal(new[] { "Vendor-1234-abc-ClientSecret" }, description.SecretNames("1234-abc.vendor.example"));
    }

    [ConfigurationType("Twice")]
    private sealed class TwoTranslations : IProvideConfiguration
    {
        [ConfigurationMember("Twice.Account", Account = true)]
        [DropSuffix]
        [OtherSuffix]
        public string Account { get; set; } = default!;
    }

    private sealed class OtherSuffixAttribute : Attribute, IProvideSecretAccountSegment
    {
        public string SecretAccountSegment(string account) => account;
    }

    [ConfigurationType("NotAccount")]
    private sealed class TranslationOffTheAccount : IProvideConfiguration
    {
        [ConfigurationMember("NotAccount.Value")]
        [DropSuffix]
        public string Value { get; set; } = default!;
    }

    [Fact]
    public void ATranslationBelongsOnTheAccountMember_AndThereIsAtMostOne()
    {
        Assert.Throws<InvalidOperationException>(() => ConfigurationMembers.For<TwoTranslations>());
        Assert.Throws<InvalidOperationException>(() => ConfigurationMembers.For<TranslationOffTheAccount>());
    }
}
