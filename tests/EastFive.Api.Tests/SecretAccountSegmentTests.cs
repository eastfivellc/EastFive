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

/// <summary>
/// The account rule at catalog time: the catalog says which member names secrets and how its
/// value becomes the segment (as-is, or through a named translation), lets an editor check a draft
/// value BEFORE writing it (the reason names the member), and refuses a type whose DEFAULT account
/// could never name a secret.
/// </summary>
public class SecretAccountRuleTests
{
    private sealed class DropDomainAttribute : Attribute, IProvideSecretAccountSegment
    {
        public string SecretAccountSegment(string account)
            => account.EndsWith(".ids.example", StringComparison.Ordinal) ? account[..^".ids.example".Length] : account;
    }

    [ConfigurationType("Plain")]
    private sealed class Plain : IProvideConfiguration
    {
        [ConfigurationMember("Plain.Account", Account = true)]
        public string Account { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        public Secret Key { get; set; } = default!;
    }

    [ConfigurationType("Translated")]
    private sealed class Translated : IProvideConfiguration
    {
        [ConfigurationMember("Translated.ClientId", Account = true)]
        [DropDomain]
        public string ClientId { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        public Secret Key { get; set; } = default!;
    }

    [ConfigurationType("BadDefault")]
    private sealed class UnsafeDefaultAccount : IProvideConfiguration
    {
        [ConfigurationMember("BadDefault.Account", Account = true, Kind = ConfigurationMemberKind.Defaulted, Default = "has.dots")]
        public string Account { get; set; } = default!;
    }

    [Fact]
    public void Catalog_MarksTheAccountMember_AndHowItsValueBecomesTheSegment()
    {
        var plain = ConfigurationCatalog.Describe(typeof(Plain)).Members.Single(member => member.IsAccount);
        Assert.Null(plain.AccountSegmentTranslation);
        var translated = ConfigurationCatalog.Describe(typeof(Translated)).Members.Single(member => member.IsAccount);
        Assert.Equal(nameof(DropDomainAttribute), translated.AccountSegmentTranslation);
        Assert.All(ConfigurationCatalog.Describe(typeof(Translated)).Members.Where(member => !member.IsAccount),
            member => Assert.Null(member.AccountSegmentTranslation));
    }

    [Theory]
    [InlineData(typeof(Plain), "acme-prod", null)]
    [InlineData(typeof(Plain), "acme.prod", "Plain.Account")]
    [InlineData(typeof(Plain), "", "Plain.Account")]
    [InlineData(typeof(Translated), "1234-abc.ids.example", null)]
    [InlineData(typeof(Translated), "1234.abc.other", "Translated.ClientId")]
    public void Catalog_ChecksADraftAccount_NamingTheMember(Type type, string account, string? problemNames)
    {
        var problem = ConfigurationCatalog.Describe(type).AccountProblem(account);
        if (problemNames == null)
            Assert.Null(problem);
        else
            Assert.Contains(problemNames, problem);
    }

    [Fact]
    public void ADefaultAccountThatCannotNameASecret_IsADeclarationError()
        => Assert.Throws<InvalidOperationException>(() => ConfigurationMembers.For<UnsafeDefaultAccount>());

    [Fact]
    public void ATypeWithoutAnAccount_HasNoAccountProblem()
        => Assert.Null(ConfigurationCatalog.Describe(typeof(Plain2)).AccountProblem("anything.at.all"));

    [ConfigurationType("Global")]
    private sealed class Plain2 : IProvideConfiguration
    {
        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        public Secret Key { get; set; } = default!;
    }
}

/// <summary>
/// The general translation, <see cref="EncodedSecretAccountSegmentAttribute"/>: any non-empty id
/// becomes a valid segment, INJECTIVELY (there is a decoder), so two ids can never share a secret.
/// A value that is already a plain segment is returned unchanged; anything else is escaped.
/// Vault names ignore letter case, so the only inputs that may meet are ones differing by case alone.
/// </summary>
public class EncodedSecretAccountSegmentTests
{
    [Theory]
    [InlineData("wh_abc", "ewh--5fabc")]
    [InlineData("_a", "e--5fa")]
    [InlineData("a.b", "ea--2eb")]
    [InlineData("-a", "e--2da")]
    [InlineData("a-", "ea--2d")]
    [InlineData("a--b", "ea--2d--2db")]
    [InlineData("a b", "ea--20b")]
    [InlineData("é", "e--c3--a9")]
    [InlineData("😀", "e--f0--9f--98--80")]
    [InlineData("1000.ABC", "e1000--2eABC")]
    [InlineData("abc\n", "eabc--0a")]
    public void AValueThatIsNotASegment_IsEscaped(string value, string segment)
    {
        Assert.Equal(segment, EncodedSecretAccountSegmentAttribute.Encode(value));
        Assert.Equal(segment, new EncodedSecretAccountSegmentAttribute().SecretAccountSegment(value));
        Assert.True(SecretNames.IsValidAccount(segment));
        Assert.True(EncodedSecretAccountSegmentAttribute.TryDecode(segment, out var decoded));
        Assert.Equal(value, decoded);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("A1")]
    [InlineData("acme-prod")]
    [InlineData("a-b-c")]
    [InlineData("e")]
    [InlineData("e5fa")]
    public void APlainSegment_IsUnchanged(string value)
    {
        Assert.Equal(value, EncodedSecretAccountSegmentAttribute.Encode(value));
        Assert.True(EncodedSecretAccountSegmentAttribute.TryDecode(value, out var decoded));
        Assert.Equal(value, decoded);
    }

    [Fact]
    public void AnEmptyValue_IsStillRefused()
    {
        Assert.Equal(string.Empty, EncodedSecretAccountSegmentAttribute.Encode(string.Empty));
        Assert.False(SecretNames.IsValidAccount(EncodedSecretAccountSegmentAttribute.Encode(string.Empty)));
    }

    [Fact]
    public void AValueThatIsNotText_IsRefused_NeverMappedLossily()
    {
        // A lone surrogate has no UTF-8 form; replacing it would let two ids share one name.
        var broken = "a\ud800b";
        Assert.Equal(broken, EncodedSecretAccountSegmentAttribute.Encode(broken));
        Assert.False(SecretNames.IsValidAccount(EncodedSecretAccountSegmentAttribute.Encode(broken)));
    }

    private static readonly string[] Alphabet =
    {
        "a", "b", "E", "e", "Z", "0", "5", "9", "f", "F", "-", "-", "_", ".", " ", "/", ":", "@", "é", "ß", "中", "😀",
    };

    private static string[] Generated(int count, int seed)
    {
        var random = new Random(seed);
        return Enumerable.Range(0, count)
            .Select(_ => string.Concat(Enumerable.Range(0, random.Next(1, 9)).Select(_ => Alphabet[random.Next(Alphabet.Length)])))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    [Fact]
    public void EveryNonEmptyValue_BecomesAValidSegment_ThatDecodesToItself()
    {
        var values = Generated(5000, seed: 20261116);
        Assert.All(values, value =>
        {
            var segment = EncodedSecretAccountSegmentAttribute.Encode(value);
            Assert.True(SecretNames.IsValidAccount(segment), $"'{value}' encoded to '{segment}', which is not a segment");
            Assert.True(EncodedSecretAccountSegmentAttribute.TryDecode(segment, out var decoded), $"'{segment}' (from '{value}') did not decode");
            Assert.Equal(value, decoded);
            if (SecretNames.IsValidAccount(value) && !value.Contains("--", StringComparison.Ordinal))
                Assert.Equal(value, segment);
        });
    }

    [Fact]
    public void TwoValuesShareASegment_OnlyWhenTheyDifferByLetterCaseAlone()
    {
        // Vault secret names ignore case, so segments are compared the way a vault compares them.
        var collisions = Generated(5000, seed: 7)
            .GroupBy(EncodedSecretAccountSegmentAttribute.Encode, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(" | ", group)}")
            .ToArray();
        Assert.Empty(collisions);
    }

    [Theory]
    [InlineData("a_b")]       // not a segment at all
    [InlineData("x--5fa")]    // escaped without the marker
    [InlineData("e--5")]      // a truncated escape
    [InlineData("e--5Fa")]    // escapes are lowercase
    [InlineData("e--zza")]    // not hex
    [InlineData("e--61")]     // a letter is never escaped, so this is nobody's encoding
    [InlineData("e--c3a")]    // not UTF-8
    [InlineData("")]
    public void Decode_RefusesWhatEncodeNeverProduces(string segment)
        => Assert.False(EncodedSecretAccountSegmentAttribute.TryDecode(segment, out _));

    [ConfigurationType("Enc")]
    private sealed class EncodedAccount : IProvideConfiguration
    {
        [ConfigurationMember("Enc.ClientId", Account = true)]
        [EncodedSecretAccountSegment]
        public string ClientId { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        public Secret Key { get; set; } = default!;
    }

    [Fact]
    public async Task OnAnAccountMember_AnyAccountNamesItsOwnSecret()
    {
        var secrets = new InMemorySecretStore(new Dictionary<string, string> { ["Enc-e1000--2eABC-Key"] = "k-encoded" });
        var outcome = await ConfigurationLoader.LoadAsync<EncodedAccount, string>(key => key == "Enc.ClientId" ? "1000.ABC" : null, secrets,
            config => $"{config.ClientId}|{config.Key.Reveal()}",
            issue => $"missing:{issue.Key}", issue => $"invalid:{issue.Key}", issue => $"failed:{issue.Key}");
        Assert.Equal("1000.ABC|k-encoded", outcome);

        var description = ConfigurationCatalog.Describe(typeof(EncodedAccount));
        Assert.Null(description.AccountProblem("1000.ABC"));
        Assert.Equal(nameof(EncodedSecretAccountSegmentAttribute), description.Members.Single(member => member.IsAccount).AccountSegmentTranslation);
    }
}
