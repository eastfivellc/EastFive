using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Xunit;

using EastFive.Configuration;

namespace EastFive.Api.Tests;

/// <summary>
/// Generated configuration values: the <see cref="IGenerateConfigurationValue"/> hook discovered on a
/// member, the catalog metadata, the built-in generators (each round-tripped through the loader that
/// consumes its format), and the runner's validation of what a generator returns.
/// </summary>
public class ConfigurationGeneratorTests
{
    /// <summary>A deterministic random source: bytes 0, 1, 2, … in order.</summary>
    private sealed class CountingRandom : RandomNumberGenerator
    {
        private byte next;

        public override void GetBytes(byte[] data)
        {
            for (var i = 0; i < data.Length; i++)
                data[i] = next++;
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A type's own generator: fills whichever slot is empty, keeping the other's value.</summary>
    private sealed class NextSlot : Attribute, IGenerateConfigurationValue
    {
        public string Label => "Next free slot";

        public Task<ConfigurationGenerationResult> GenerateAsync(ConfigurationGenerationContext context)
        {
            var target = string.IsNullOrEmpty(context.DraftValue(nameof(Rotating.Key1))) ? nameof(Rotating.Key1) : nameof(Rotating.Key2);
            return Task.FromResult(ConfigurationGenerationResult.Of(target, Convert.ToHexString(context.RandomBytes(4)))
                .WithValue(nameof(Rotating.Active), target)
                .WithOutput("Slot", target)
                .WithInstruction("Deploy, then retire the other slot."));
        }
    }

    private sealed class Stray : Attribute, IGenerateConfigurationValue
    {
        public string Label => "Names another type's member";

        public Task<ConfigurationGenerationResult> GenerateAsync(ConfigurationGenerationContext context)
            => Task.FromResult(ConfigurationGenerationResult.Of("ApiKey", "x"));
    }

    private sealed class BadGuid : Attribute, IGenerateConfigurationValue
    {
        public string Label => "Not a GUID";

        public Task<ConfigurationGenerationResult> GenerateAsync(ConfigurationGenerationContext context)
            => Task.FromResult(ConfigurationGenerationResult.Of(nameof(Rotating.Instance), "not-a-guid"));
    }

    [ConfigurationType("Rotating", Label = "Rotating", Description = "Two slots.")]
    private sealed class Rotating : IProvideConfiguration
    {
        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        [NextSlot]
        public Secret Key1 { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        public Secret? Key2 { get; set; }

        [ConfigurationMember("Rotating.Active", Kind = ConfigurationMemberKind.Optional)]
        [Stray]
        public string? Active { get; set; }

        [ConfigurationMember("Rotating.Instance", Kind = ConfigurationMemberKind.Optional)]
        [BadGuid]
        public Guid? Instance { get; set; }

        [ConfigurationMember("Rotating.Plain", Kind = ConfigurationMemberKind.Optional)]
        public string? Plain { get; set; }
    }

    [ConfigurationType("Keys", Label = "Keys", Description = "Built-ins.")]
    private sealed class Keys : IProvideConfiguration
    {
        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        [GenerateRandomBytes(32)]
        public Secret Url { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        [GenerateRandomBytes(16, Encoding = ConfigurationRandomEncoding.Hex)]
        public Secret Hex { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        [GenerateRandomBytes(24, Encoding = ConfigurationRandomEncoding.Base64, Instruction = "Paste it in the vendor portal.", InstructionLink = "https://vendor.example/keys")]
        public Secret Classic { get; set; } = default!;

        [ConfigurationMember("Keys.Id", Kind = ConfigurationMemberKind.Optional)]
        [GenerateGuid]
        public Guid? Id { get; set; }

        [ConfigurationMember("Keys.Name", Kind = ConfigurationMemberKind.Optional)]
        [GenerateGuid(Format = "N")]
        public string? Name { get; set; }

        [ConfigurationMember("Keys.RsaKeyId", Kind = ConfigurationMemberKind.Optional)]
        public string? RsaKeyId { get; set; }
    }

    [ConfigurationType("Doubled", Label = "Doubled", Description = "Two generators.")]
    private sealed class Doubled : IProvideConfiguration
    {
        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        [GenerateRandomBytes(32)]
        [NextSlot]
        public Secret Key { get; set; } = default!;
    }

    private static ConfigurationGenerationContext Context<TConfig>(string member, IReadOnlyDictionary<string, string?>? draft = null)
        where TConfig : IProvideConfiguration
        => new(typeof(TConfig), ConfigurationMembers.For<TConfig>().Members.Single(candidate => candidate.Property.Name == member),
            draft ?? new Dictionary<string, string?>(), Environment: "Sandbox")
        {
            Random = new CountingRandom(),
            Clock = new FixedClock(Now),
        };

    private static Task<ConfigurationGenerationResult> GenerateAsync<TConfig>(string member, IReadOnlyDictionary<string, string?>? draft = null)
        where TConfig : IProvideConfiguration
        => ConfigurationGeneration.GenerateAsync(Context<TConfig>(member, draft),
            result => result,
            why => throw new Xunit.Sdk.XunitException($"no generator: {why}"),
            why => throw new Xunit.Sdk.XunitException($"refused: {why}"));

    // ---- catalog metadata ----

    [Fact]
    public void A_members_generator_is_discovered_by_interface_and_described_by_its_label()
    {
        var members = ConfigurationMembers.For<Keys>().Members;
        Assert.IsType<GenerateRandomBytesAttribute>(members.Single(member => member.Property.Name == nameof(Keys.Url)).Generator);
        Assert.Null(members.Single(member => member.Property.Name == nameof(Keys.RsaKeyId)).Generator);

        var described = ConfigurationCatalog.Describe(typeof(Keys)).Members;
        Assert.Equal("Random 32 bytes, base64url", described.Single(member => member.Property == nameof(Keys.Url)).GeneratorLabel);
        Assert.Null(described.Single(member => member.Property == nameof(Keys.RsaKeyId)).GeneratorLabel);
    }

    [Fact]
    public void Two_generators_on_one_member_is_a_declaration_error()
    {
        var error = Assert.Throws<InvalidOperationException>(() => ConfigurationMembers.For<Doubled>());
        Assert.Contains("more than one generator", error.Message);
        Assert.Throws<InvalidOperationException>(() => ConfigurationCatalog.Describe(typeof(Doubled)));
    }

    // ---- built-ins ----

    [Fact]
    public async Task Random_bytes_encode_as_declared()
    {
        var url = (await GenerateAsync<Keys>(nameof(Keys.Url))).Values[nameof(Keys.Url)];
        Assert.Equal(Base64Url(Enumerable.Range(0, 32).Select(i => (byte)i).ToArray()), url);
        Assert.Equal(43, url.Length);
        Assert.DoesNotContain('=', url);

        var hex = (await GenerateAsync<Keys>(nameof(Keys.Hex))).Values[nameof(Keys.Hex)];
        Assert.Equal("000102030405060708090a0b0c0d0e0f", hex);

        var classic = await GenerateAsync<Keys>(nameof(Keys.Classic));
        Assert.Equal(24, Convert.FromBase64String(classic.Values[nameof(Keys.Classic)]).Length);
        var instruction = Assert.Single(classic.Instructions);
        Assert.Equal("Paste it in the vendor portal.", instruction.Text);
        Assert.Equal("https://vendor.example/keys", instruction.Link);
        Assert.Empty(classic.Outputs);
    }

    [Fact]
    public async Task A_guid_is_a_version_4_guid_in_the_declared_format()
    {
        var id = (await GenerateAsync<Keys>(nameof(Keys.Id))).Values[nameof(Keys.Id)];
        Assert.True(Guid.TryParseExact(id, "D", out var parsed));
        Assert.Equal(4, parsed.Version);

        var name = (await GenerateAsync<Keys>(nameof(Keys.Name))).Values[nameof(Keys.Name)];
        Assert.Matches("^[0-9a-f]{32}$", name);
    }

    [Fact]
    public void A_built_in_refuses_an_impossible_size_when_declared()
    {
        Assert.ThrowsAny<ArgumentException>(() => new EastFive.Security.SymmetricKeyAttribute(0));
        Assert.ThrowsAny<ArgumentException>(() => new EastFive.Security.RsaKeyAttribute(1024));
        Assert.ThrowsAny<ArgumentException>(() => new GenerateRandomBytesAttribute(0));
    }

    // ---- a type's own generator, and the runner ----

    [Fact]
    public async Task A_types_generator_fills_several_members_from_the_draft()
    {
        var first = await GenerateAsync<Rotating>(nameof(Rotating.Key1));
        Assert.Equal(new[] { nameof(Rotating.Active), nameof(Rotating.Key1) }, first.Values.Keys.OrderBy(key => key));
        Assert.Equal(nameof(Rotating.Key1), first.Values[nameof(Rotating.Active)]);

        var second = await GenerateAsync<Rotating>(nameof(Rotating.Key1),
            new Dictionary<string, string?> { [nameof(Rotating.Key1)] = "kept" });
        Assert.Equal("00010203", second.Values[nameof(Rotating.Key2)]);
        Assert.False(second.Values.ContainsKey(nameof(Rotating.Key1)));
        Assert.Equal("Slot", Assert.Single(second.Outputs).Label);
        Assert.Equal("Deploy, then retire the other slot.", Assert.Single(second.Instructions).Text);
    }

    [Fact]
    public async Task A_result_naming_a_member_the_type_does_not_declare_is_refused()
    {
        var refusal = await ConfigurationGeneration.GenerateAsync(Context<Rotating>(nameof(Rotating.Active)),
            _ => (string?)null, why => throw new Xunit.Sdk.XunitException(why), why => why);
        Assert.Contains("ApiKey", refusal);
        Assert.Contains("Rotating", refusal);
    }

    [Fact]
    public async Task A_value_the_member_cannot_hold_is_refused_without_echoing_it()
    {
        var refusal = await ConfigurationGeneration.GenerateAsync(Context<Rotating>(nameof(Rotating.Instance)),
            _ => (string?)null, why => throw new Xunit.Sdk.XunitException(why), why => why);
        Assert.Contains(nameof(Rotating.Instance), refusal);
        Assert.DoesNotContain("not-a-guid", refusal);
    }

    [Fact]
    public async Task A_member_without_a_generator_says_so()
    {
        var why = await ConfigurationGeneration.GenerateAsync(Context<Rotating>(nameof(Rotating.Plain)),
            _ => (string?)null, reason => reason, refused => throw new Xunit.Sdk.XunitException(refused));
        Assert.Contains("no generator", why);
    }

    [Fact]
    public void Neither_the_context_nor_a_result_prints_a_value()
    {
        var context = Context<Rotating>(nameof(Rotating.Key1),
            new Dictionary<string, string?> { [nameof(Rotating.Key1)] = "draft-secret-value" });
        Assert.DoesNotContain("draft-secret-value", context.ToString());
        var result = ConfigurationGenerationResult.Of("Key1", "generated-secret-value").WithOutput("Out", "output-value");
        Assert.DoesNotContain("generated-secret-value", result.ToString());
        Assert.DoesNotContain("output-value", result.ToString());
    }

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string text)
    {
        var padded = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded + new string('=', (4 - padded.Length % 4) % 4));
    }
}
