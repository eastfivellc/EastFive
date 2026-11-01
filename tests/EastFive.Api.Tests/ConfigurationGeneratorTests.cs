using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Xunit;

using EastFive.Configuration;
using ECDSA = EastFive.Security.ECDSA;

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

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        [GenerateRsaKey(KeyIdMember = nameof(RsaKeyId))]
        public Secret RsaPem { get; set; } = default!;

        [ConfigurationMember("Keys.RsaKeyId", Kind = ConfigurationMemberKind.Optional)]
        public string? RsaKeyId { get; set; }

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        [GenerateRsaKey(3072, Format = ConfigurationRsaKeyFormat.RsaXmlBase64, Algorithm = "RS384")]
        public Secret RsaXml { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        [GenerateEcKey]
        public Secret EcPem { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        [GenerateEcKey(ConfigurationEcCurve.P384, Format = ConfigurationEcKeyFormat.EcPrivateKeyBase64)]
        public Secret EcSec1 { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        [GenerateAesKey]
        public Secret Aes { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        [GenerateAesKey(128, Encoding = ConfigurationRandomEncoding.Hex)]
        public Secret Aes128 { get; set; } = default!;
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

    private static JsonElement SingleJwk(ConfigurationGenerationResult result)
    {
        var jwks = result.Outputs.Single(output => output.Kind == ConfigurationOutputKind.Json);
        return JsonDocument.Parse(jwks.Value).RootElement.GetProperty("keys").EnumerateArray().Single();
    }

    // ---- catalog metadata ----

    [Fact]
    public void A_members_generator_is_discovered_by_interface_and_described_by_its_label()
    {
        var members = ConfigurationMembers.For<Keys>().Members;
        Assert.IsType<GenerateRandomBytesAttribute>(members.Single(member => member.Property.Name == nameof(Keys.Url)).Generator);
        Assert.Null(members.Single(member => member.Property.Name == nameof(Keys.RsaKeyId)).Generator);

        var described = ConfigurationCatalog.Describe(typeof(Keys)).Members;
        Assert.Equal("Random 32 bytes, base64url", described.Single(member => member.Property == nameof(Keys.Url)).GeneratorLabel);
        Assert.Equal("RSA 2048 key pair (PEM)", described.Single(member => member.Property == nameof(Keys.RsaPem)).GeneratorLabel);
        Assert.Equal("EC P-384 key pair (base64 EC private key)", described.Single(member => member.Property == nameof(Keys.EcSec1)).GeneratorLabel);
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
    public async Task An_rsa_pem_key_round_trips_and_its_jwks_and_public_key_match_it()
    {
        var result = await GenerateAsync<Keys>(nameof(Keys.RsaPem));
        using var rsa = RSA.Create();
        rsa.ImportFromPem(result.Values[nameof(Keys.RsaPem)]);
        Assert.Equal(2048, rsa.KeySize);
        var parameters = rsa.ExportParameters(false);

        using var published = RSA.Create();
        published.ImportFromPem(result.Outputs.Single(output => output.Kind == ConfigurationOutputKind.Pem).Value);
        Assert.Equal(parameters.Modulus, published.ExportParameters(false).Modulus);

        var jwk = SingleJwk(result);
        Assert.Equal("RSA", jwk.GetProperty("kty").GetString());
        Assert.Equal("RS256", jwk.GetProperty("alg").GetString());
        Assert.Equal("sig", jwk.GetProperty("use").GetString());
        Assert.Equal(Base64Url(parameters.Modulus!), jwk.GetProperty("n").GetString());
        Assert.Equal(Base64Url(parameters.Exponent!), jwk.GetProperty("e").GetString());

        // The key id fills the named member, dated by the clock, and is the JWK's kid.
        var keyId = result.Values[nameof(Keys.RsaKeyId)];
        Assert.StartsWith("2026-11-01-", keyId);
        Assert.Equal(keyId, jwk.GetProperty("kid").GetString());
    }

    [Fact]
    public async Task An_rsa_xml_key_round_trips_through_the_eastfive_loader()
    {
        var result = await GenerateAsync<Keys>(nameof(Keys.RsaXml));
        var size = EastFive.Security.RSA.RSAFromSecret(new Secret(result.Values[nameof(Keys.RsaXml)]),
            rsa => { using (rsa) { return rsa.KeySize; } },
            why => throw new Xunit.Sdk.XunitException(why));
        Assert.Equal(3072, size);
        Assert.Equal("RS384", SingleJwk(result).GetProperty("alg").GetString());
        // No key-id member: the kid is an output to copy instead.
        Assert.False(result.Values.ContainsKey(nameof(Keys.RsaKeyId)));
        Assert.Contains(result.Outputs, output => output.Label == "Key id" && output.Value.StartsWith("2026-11-01-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_token_signing_and_voucher_keys_generate_what_their_loaders_read()
    {
        var tokens = await ConfigurationGeneration.GenerateAsync(
            new ConfigurationGenerationContext(typeof(EastFive.Security.Tokens.TokenSigningConfiguration),
                ConfigurationMembers.For<EastFive.Security.Tokens.TokenSigningConfiguration>().Members.Single(member => member.Property.Name == "SigningKey"),
                new Dictionary<string, string?>()),
            result => result, why => throw new Xunit.Sdk.XunitException(why), why => throw new Xunit.Sdk.XunitException(why));
        var signing = new EastFive.Security.Tokens.TokenSigningConfiguration
        {
            Issuer = "https://issuer.example",
            SigningKey = new Secret(tokens.Values["SigningKey"]),
        };
        Assert.Null(signing.Validate());
        Assert.Equal(2048, signing.ToSigning().WithRsa(rsa => { using (rsa) { return rsa.KeySize; } }, why => throw new Xunit.Sdk.XunitException(why)));
        Assert.Single(tokens.Instructions);

        var vouchers = await ConfigurationGeneration.GenerateAsync(
            new ConfigurationGenerationContext(typeof(EastFive.Security.VoucherConfiguration),
                ConfigurationMembers.For<EastFive.Security.VoucherConfiguration>().Members.Single(member => member.Property.Name == "SigningKey"),
                new Dictionary<string, string?>()),
            result => result, why => throw new Xunit.Sdk.XunitException(why), why => throw new Xunit.Sdk.XunitException(why));
        Assert.True(EastFive.Security.RSA.RSAFromSecret(new Secret(vouchers.Values["SigningKey"]),
            rsa => { using (rsa) { return true; } }, _ => false));
    }

    [Fact]
    public async Task An_ec_key_round_trips_in_each_format_and_its_jwk_names_the_curve()
    {
        var pem = await GenerateAsync<Keys>(nameof(Keys.EcPem));
        using (var ecdsa = ECDsa.Create())
        {
            ecdsa.ImportFromPem(pem.Values[nameof(Keys.EcPem)]);
            Assert.Equal(256, ecdsa.KeySize);
        }
        Assert.Equal("P-256", SingleJwk(pem).GetProperty("crv").GetString());
        Assert.Equal("ES256", SingleJwk(pem).GetProperty("alg").GetString());

        var sec1 = await GenerateAsync<Keys>(nameof(Keys.EcSec1));
        var alg = ECDSA.FromPrivateKey(Convert.FromBase64String(sec1.Values[nameof(Keys.EcSec1)]),
            (ecdsa, algorithm) => { using (ecdsa) { return algorithm; } },
            why => throw new Xunit.Sdk.XunitException(why));
        Assert.Equal("ES384", alg);
        var jwk = SingleJwk(sec1);
        Assert.Equal("EC", jwk.GetProperty("kty").GetString());
        Assert.Equal("P-384", jwk.GetProperty("crv").GetString());
        Assert.Equal(48, Base64UrlDecode(jwk.GetProperty("x").GetString()!).Length);
    }

    [Fact]
    public async Task An_aes_key_is_the_declared_size()
    {
        var key = (await GenerateAsync<Keys>(nameof(Keys.Aes))).Values[nameof(Keys.Aes)];
        using var aes = System.Security.Cryptography.Aes.Create();
        aes.Key = Convert.FromBase64String(key);
        Assert.Equal(256, aes.KeySize);

        Assert.Equal(32, (await GenerateAsync<Keys>(nameof(Keys.Aes128))).Values[nameof(Keys.Aes128)].Length);
    }

    [Fact]
    public void A_built_in_refuses_an_impossible_size_when_declared()
    {
        Assert.ThrowsAny<ArgumentException>(() => new GenerateAesKeyAttribute(100));
        Assert.ThrowsAny<ArgumentException>(() => new GenerateRsaKeyAttribute(1024));
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
