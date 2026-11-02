using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Xunit;

using EastFive.Configuration;
using EastFive.Security;

namespace EastFive.Api.Tests;

/// <summary>
/// Typed key values: <see cref="RsaPrivateKey"/>, <see cref="EcPrivateKey"/>, <see cref="SymmetricKey"/> and
/// <see cref="SharedSecret"/>, the member attributes that constrain, parse and generate them, and the
/// <see cref="IParseConfigurationValue"/> hook the loader, inspector and catalog bind through.
/// </summary>
public class TypedKeyValueTests
{
    private static string B64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string B64(string text) => Convert.ToBase64String(Encoding.ASCII.GetBytes(text));

    private static string RsaJwk(System.Security.Cryptography.RSA rsa, bool includePrivate = true)
    {
        var p = rsa.ExportParameters(includePrivate);
        var jwk = new Dictionary<string, string> { ["kty"] = "RSA", ["n"] = B64Url(p.Modulus!), ["e"] = B64Url(p.Exponent!) };
        if (includePrivate)
        {
            jwk["d"] = B64Url(p.D!);
            jwk["p"] = B64Url(p.P!);
            jwk["q"] = B64Url(p.Q!);
            jwk["dp"] = B64Url(p.DP!);
            jwk["dq"] = B64Url(p.DQ!);
            jwk["qi"] = B64Url(p.InverseQ!);
        }
        return JsonSerializer.Serialize(jwk);
    }

    private static string EcJwk(ECDsa ecdsa, string crv, bool includePrivate = true)
    {
        var p = ecdsa.ExportParameters(includePrivate);
        var jwk = new Dictionary<string, string> { ["kty"] = "EC", ["crv"] = crv, ["x"] = B64Url(p.Q.X!), ["y"] = B64Url(p.Q.Y!) };
        if (includePrivate)
            jwk["d"] = B64Url(p.D!);
        return JsonSerializer.Serialize(jwk);
    }

    private static RsaPrivateKey ParseRsa(string text, int minBits = 2048)
        => RsaPrivateKey.Parse(text, key => key, why => throw new Xunit.Sdk.XunitException(why), minBits);

    private static string RsaProblem(string text, int minBits = 2048)
        => RsaPrivateKey.Parse(text, _ => throw new Xunit.Sdk.XunitException("parsed"), why => why, minBits);

    private static EcPrivateKey ParseEc(string text, ConfigurationEcCurve? curve = null)
        => EcPrivateKey.Parse(text, key => key, why => throw new Xunit.Sdk.XunitException(why), curve);

    private static string EcProblem(string text, ConfigurationEcCurve? curve = null)
        => EcPrivateKey.Parse(text, _ => throw new Xunit.Sdk.XunitException("parsed"), why => why, curve);

    #region RSA

    [Fact]
    public void Every_rsa_encoding_parses_to_the_same_key()
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var encodings = new Dictionary<string, string>
        {
            ["PKCS#8 PEM"] = rsa.ExportPkcs8PrivateKeyPem(),
            ["PKCS#1 PEM"] = rsa.ExportRSAPrivateKeyPem(),
            ["PEM with escaped newlines"] = rsa.ExportPkcs8PrivateKeyPem().Replace("\n", "\\n"),
            ["base64 PKCS#8 DER"] = Convert.ToBase64String(rsa.ExportPkcs8PrivateKey()),
            ["base64 PKCS#1 DER"] = Convert.ToBase64String(rsa.ExportRSAPrivateKey()),
            ["base64 .NET XML"] = B64(rsa.ToXmlString(true)),
            ["private JWK"] = RsaJwk(rsa),
        };
        var expected = B64Url(rsa.ExportParameters(false).Modulus!);

        foreach (var (format, text) in encodings)
        {
            var key = ParseRsa(text);
            Assert.True(expected == key.PublicJwk()["n"], format);
            Assert.Equal(2048, key.KeySize);
            Assert.Equal("RS256", key.Algorithm);
        }
        Assert.Single(encodings.Values.Select(text => ParseRsa(text).Thumbprint).Distinct());
    }

    [Fact]
    public void The_parsed_rsa_key_signs_what_its_public_jwk_verifies()
    {
        using var source = System.Security.Cryptography.RSA.Create(2048);
        var key = ParseRsa(source.ExportPkcs8PrivateKeyPem());
        var data = Encoding.UTF8.GetBytes("payload");
        using var signer = key.CreateRsa();
        var signature = signer.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        Assert.True(source.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

    [Fact]
    public void An_rsa_key_below_the_minimum_is_refused_naming_both_sizes()
    {
        using var small = System.Security.Cryptography.RSA.Create(1024);
        Assert.Equal("RSA 1024 < 2048", RsaProblem(small.ExportPkcs8PrivateKeyPem()));
        Assert.Equal(1024, ParseRsa(small.ExportPkcs8PrivateKeyPem(), minBits: 1024).KeySize);
    }

    [Fact]
    public void A_public_rsa_key_is_refused_as_public_only_in_every_encoding()
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        Assert.Contains("public key only", RsaProblem(rsa.ExportSubjectPublicKeyInfoPem()));
        Assert.Contains("public key only", RsaProblem(rsa.ExportRSAPublicKeyPem()));
        Assert.Contains("public key only", RsaProblem(Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo())));
        Assert.Contains("public key only", RsaProblem(B64(rsa.ToXmlString(false))));
        Assert.Contains("public key only", RsaProblem(RsaJwk(rsa, includePrivate: false)));
    }

    [Fact]
    public void An_ec_key_where_rsa_is_expected_names_what_it_got()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        Assert.Equal("expected RSA private key; got EC P-256 private key", RsaProblem(ec.ExportPkcs8PrivateKeyPem()));
        Assert.Equal("expected RSA private key; got EC P-256 private key", RsaProblem(Convert.ToBase64String(ec.ExportECPrivateKey())));
    }

    [Fact]
    public void Text_that_is_no_key_is_refused_without_echoing_it()
    {
        const string garbage = "definitely-not-a-key-1234";
        var problem = RsaProblem(garbage);
        Assert.StartsWith("expected RSA private key", problem);
        Assert.DoesNotContain(garbage, problem);
        Assert.StartsWith("expected EC private key", EcProblem(garbage));
    }

    [Fact]
    public void An_rsa_key_prints_its_kind_never_its_material()
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var key = ParseRsa(rsa.ExportPkcs8PrivateKeyPem());
        Assert.Equal("RSA 2048 private key", key.ToString());
        var d = rsa.ExportParameters(true).D!;
        foreach (var serialized in new[] { Newtonsoft.Json.JsonConvert.SerializeObject(key), JsonSerializer.Serialize(key) })
        {
            Assert.DoesNotContain(B64Url(d), serialized);
            Assert.DoesNotContain(Convert.ToBase64String(d), serialized);
            Assert.DoesNotContain("PRIVATE KEY", serialized);
        }
    }

    #endregion

    #region EC

    [Fact]
    public void Every_ec_encoding_parses_to_the_same_key()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        var encodings = new Dictionary<string, string>
        {
            ["PKCS#8 PEM"] = ec.ExportPkcs8PrivateKeyPem(),
            ["SEC1 PEM"] = ec.ExportECPrivateKeyPem(),
            ["base64 PKCS#8 DER"] = Convert.ToBase64String(ec.ExportPkcs8PrivateKey()),
            ["base64 SEC1 DER (today's Epic format)"] = Convert.ToBase64String(ec.ExportECPrivateKey()),
            ["private JWK"] = EcJwk(ec, "P-384"),
        };

        foreach (var (format, text) in encodings)
        {
            var key = ParseEc(text, ConfigurationEcCurve.P384);
            Assert.True(key.Curve == "P-384", format);
            Assert.Equal("ES384", key.Algorithm);
        }
        Assert.Single(encodings.Values.Select(text => ParseEc(text).Thumbprint).Distinct());
    }

    [Theory]
    [InlineData(ConfigurationEcCurve.P256, "P-256", "ES256")]
    [InlineData(ConfigurationEcCurve.P384, "P-384", "ES384")]
    [InlineData(ConfigurationEcCurve.P521, "P-521", "ES512")]
    public void The_algorithm_follows_the_curve(ConfigurationEcCurve curve, string name, string algorithm)
    {
        var key = EcPrivateKey.Generate(curve);
        Assert.Equal(name, key.Curve);
        Assert.Equal(algorithm, key.Algorithm);
        Assert.Equal(algorithm, key.PublicJwk()["alg"]);
    }

    [Fact]
    public void The_wrong_curve_is_refused_naming_both()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        Assert.Equal("expected EC P-384 private key; got P-256", EcProblem(ec.ExportPkcs8PrivateKeyPem(), ConfigurationEcCurve.P384));
    }

    [Fact]
    public void A_public_ec_key_or_an_rsa_key_is_refused()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        Assert.Contains("public key only", EcProblem(ec.ExportSubjectPublicKeyInfoPem()));
        Assert.Contains("public key only", EcProblem(EcJwk(ec, "P-384", includePrivate: false)));
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        Assert.Equal("expected EC private key; got RSA 2048 private key", EcProblem(rsa.ExportPkcs8PrivateKeyPem()));
        Assert.Equal("expected EC private key; got RSA 2048 private key", EcProblem(B64(rsa.ToXmlString(true))));
    }

    [Fact]
    public void An_ec_jwk_is_base64url_and_signatures_verify_against_it()
    {
        var key = EcPrivateKey.Generate(ConfigurationEcCurve.P384);
        var jwk = key.PublicJwk();
        foreach (var member in new[] { "x", "y", "kid" })
            Assert.DoesNotMatch("[+/=]", jwk[member]);
        Assert.False(jwk.ContainsKey("d"));
        Assert.Equal(key.Thumbprint, jwk["kid"]);

        var data = Encoding.UTF8.GetBytes("payload");
        using var signer = key.CreateEcdsa();
        var signature = signer.SignData(data, HashAlgorithmName.SHA384);
        using var verifier = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP384,
            Q = new ECPoint { X = FromB64Url(jwk["x"]), Y = FromB64Url(jwk["y"]) },
        });
        Assert.True(verifier.VerifyData(data, signature, HashAlgorithmName.SHA384));
    }

    private static byte[] FromB64Url(string text)
    {
        var s = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }

    #endregion

    #region Thumbprints

    /// <summary>RFC 7638 §3.1's example key and its thumbprint.</summary>
    [Fact]
    public void The_rsa_thumbprint_matches_the_rfc_7638_vector()
    {
        const string n = "0vx7agoebGcQSuuPiLJXZptN9nndrQmbXEps2aiAFbWhM78LhWx4cbbfAAtVT86zwu1RK7aPFFxuhDR1L6tSoc_BJECPebWKRXjBZCiFV4n3oknjhMstn64tZ_2W-5JsGY4Hc5n9yBXArwl93lqt7_RN5w6Cf0h4QyQ5v-65YGjQR0_FDW2QvzqY368QQMicAtaSqzs8KJZgnYb9c7d0zgdAZHzu6qMQvRL5hajrn1n91CbOpbISD08qNLyrdkt-bFTWhAI4vMQFh6WeZu0fM4lFd2NcRwr3XPksINHaQ-G_xBniIqbw0Ls1jF44-csFCur-kEgU8awapJzKnqDKgw";
        Assert.Equal("NzbLsXh8uDCcd-6MNwXF4W_7noWXFZAfHkxZsRGC9Xs", JsonWebKeyThumbprint.Rsa(n, "AQAB"));
    }

    [Fact]
    public void Thumbprints_agree_with_identity_model()
    {
        var rsa = RsaPrivateKey.Generate();
        var rsaJwk = new Microsoft.IdentityModel.Tokens.JsonWebKey(JsonSerializer.Serialize(rsa.PublicJwk()));
        Assert.Equal(B64Url(rsaJwk.ComputeJwkThumbprint()), rsa.Thumbprint);

        var ec = EcPrivateKey.Generate(ConfigurationEcCurve.P384);
        var ecJwk = new Microsoft.IdentityModel.Tokens.JsonWebKey(JsonSerializer.Serialize(ec.PublicJwk()));
        Assert.Equal(B64Url(ecJwk.ComputeJwkThumbprint()), ec.Thumbprint);
    }

    #endregion

    #region Symmetric keys and shared secrets

    [Fact]
    public void A_symmetric_key_reads_only_its_declared_encoding_at_its_exact_length()
    {
        var bytes = Enumerable.Range(0, 32).Select(i => (byte)(i * 7)).ToArray();
        byte[] Parse(string text, ConfigurationRandomEncoding encoding, int length = 32)
            => SymmetricKey.Parse(text, length, encoding, key => key.ToArray(), why => throw new Xunit.Sdk.XunitException(why));
        string Problem(string text, ConfigurationRandomEncoding encoding, int length = 32)
            => SymmetricKey.Parse(text, length, encoding, _ => throw new Xunit.Sdk.XunitException("parsed"), why => why);

        Assert.Equal(bytes, Parse(Convert.ToBase64String(bytes), ConfigurationRandomEncoding.Base64));
        Assert.Equal(bytes, Parse(B64Url(bytes), ConfigurationRandomEncoding.Base64Url));
        Assert.Equal(bytes, Parse(Convert.ToHexString(bytes), ConfigurationRandomEncoding.Hex));
        Assert.Equal(bytes, Parse(Convert.ToHexString(bytes).ToLowerInvariant(), ConfigurationRandomEncoding.Hex));

        Assert.Equal("expected 32-byte key (base64); got 16 bytes", Problem(Convert.ToBase64String(bytes[..16]), ConfigurationRandomEncoding.Base64));
        Assert.Equal("expected 32-byte key (base64); the value is not base64", Problem(B64Url(bytes) + "-_", ConfigurationRandomEncoding.Base64));
        Assert.Equal("expected 32-byte key (hex); the value is not hex", Problem(Convert.ToBase64String(bytes), ConfigurationRandomEncoding.Hex));
    }

    [Fact]
    public void A_symmetric_key_hands_out_copies_and_prints_no_bytes()
    {
        var key = SymmetricKey.Parse(Convert.ToBase64String(new byte[16]), 16, ConfigurationRandomEncoding.Base64, k => k, why => throw new Xunit.Sdk.XunitException(why));
        key.ToArray()[0] = 9;
        Assert.Equal(0, key.ToArray()[0]);
        Assert.Equal(16, key.Length);
        Assert.Equal("[16-byte key]", key.ToString());
        Assert.Equal("{\"Length\":16}", JsonSerializer.Serialize(key));
        Assert.Equal("{\"Length\":16}", Newtonsoft.Json.JsonConvert.SerializeObject(key));
    }

    [Fact]
    public void A_shared_secret_matches_only_its_own_value()
    {
        var value = new string('s', 40);
        var secret = SharedSecret.Parse(value, 32, s => s, why => throw new Xunit.Sdk.XunitException(why));
        Assert.True(secret.Matches(value));
        Assert.False(secret.Matches(value + "x"));
        Assert.False(secret.Matches(value[..39]));
        Assert.False(secret.Matches(null));
        Assert.False(secret.Matches(string.Empty));
        Assert.Equal(value, secret.Reveal());
        Assert.Equal(Encoding.UTF8.GetBytes(value), secret.Utf8Bytes());
        Assert.Equal("[secret]", secret.ToString());
        Assert.Equal("{}", JsonSerializer.Serialize(secret));
    }

    [Fact]
    public void A_short_shared_secret_is_refused_naming_the_minimum()
    {
        Assert.Equal("expected a shared secret of at least 32 bytes; got 5",
            SharedSecret.Parse("short", 32, _ => "parsed", why => why));
        Assert.Equal("parsed", SharedSecret.Parse("short", 5, _ => "parsed", why => why));
    }

    #endregion

    #region The configuration hook

    [ConfigurationType("Typed", Label = "Typed", Description = "Typed key values.")]
    private sealed class Typed : IProvideConfiguration
    {
        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        [RsaKey(KeyIdMember = nameof(RsaKeyId))]
        public RsaPrivateKey Rsa { get; set; } = default!;

        [ConfigurationMember("Typed.RsaKeyId", Kind = ConfigurationMemberKind.Optional)]
        public string? RsaKeyId { get; set; }

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        [EcKey(ConfigurationEcCurve.P384, Instruction = "Upload the JWKS.")]
        public EcPrivateKey Ec { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        [SymmetricKey(32)]
        public SymmetricKey? Aes { get; set; }

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        [SharedSecret]
        public SharedSecret? Webhook { get; set; }
    }

    private sealed class Unattributed : IProvideConfiguration
    {
        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        public RsaPrivateKey Rsa { get; set; } = default!;
    }

    private sealed class InTheValuesStore : IProvideConfiguration
    {
        [ConfigurationMember("Values.Key")]
        [SharedSecret]
        public SharedSecret Key { get; set; } = default!;
    }

    private sealed class WrongType : IProvideConfiguration
    {
        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        [EcKey(ConfigurationEcCurve.P256)]
        public RsaPrivateKey Rsa { get; set; } = default!;
    }

    private sealed class TwoParsers : IProvideConfiguration
    {
        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        [SharedSecret]
        [SymmetricKey(32)]
        public SharedSecret Key { get; set; } = default!;
    }

    [Fact]
    public void A_typed_member_needs_its_attribute_in_the_secret_store_on_a_matching_type()
    {
        Assert.Contains("RsaPrivateKey", Assert.Throws<InvalidOperationException>(() => ConfigurationMembers.For(typeof(Unattributed))).Message);
        Assert.Contains("secret material", Assert.Throws<InvalidOperationException>(() => ConfigurationMembers.For(typeof(InTheValuesStore))).Message);
        Assert.Contains("EcPrivateKey", Assert.Throws<InvalidOperationException>(() => ConfigurationMembers.For(typeof(WrongType))).Message);
        Assert.Contains("more than one parser", Assert.Throws<InvalidOperationException>(() => ConfigurationMembers.For(typeof(TwoParsers))).Message);
    }

    [Fact]
    public void The_catalog_describes_each_members_kind_and_accepted_formats()
    {
        var members = ConfigurationCatalog.Describe(typeof(Typed)).Members.ToDictionary(member => member.Property);
        Assert.Equal("RSA private key (2048+ bits)", members[nameof(Typed.Rsa)].ValueKind);
        Assert.Equal("EC P-384 private key", members[nameof(Typed.Ec)].ValueKind);
        Assert.Equal(new[] { "PEM (PKCS#8 or SEC1)", "base64 DER (PKCS#8 or SEC1)", "JWK (private)" }, members[nameof(Typed.Ec)].AcceptedFormats);
        Assert.Contains("base64 .NET RSA XML", members[nameof(Typed.Rsa)].AcceptedFormats!);
        Assert.Equal("32-byte symmetric key", members[nameof(Typed.Aes)].ValueKind);
        Assert.Equal(new[] { "base64" }, members[nameof(Typed.Aes)].AcceptedFormats);
        Assert.Equal("Shared secret (32+ bytes)", members[nameof(Typed.Webhook)].ValueKind);
        Assert.Null(members[nameof(Typed.RsaKeyId)].ValueKind);
        Assert.Null(members[nameof(Typed.RsaKeyId)].AcceptedFormats);
        Assert.Equal(ConfigurationFieldKind.Secret, members[nameof(Typed.Rsa)].Metadata.FieldKind);
    }

    private static Dictionary<string, string> TypedDraft(string? rsa = null, string? ec = null)
    {
        using var rsaKey = System.Security.Cryptography.RSA.Create(2048);
        using var ecKey = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        return new Dictionary<string, string>
        {
            ["Typed-Rsa"] = rsa ?? B64(rsaKey.ToXmlString(true)),
            ["Typed-Ec"] = ec ?? Convert.ToBase64String(ecKey.ExportECPrivateKey()),
            ["Typed-Aes"] = Convert.ToBase64String(new byte[32]),
            ["Typed-Webhook"] = new string('w', 32),
        };
    }

    [Fact]
    public void The_loader_parses_typed_members_from_todays_encodings()
    {
        var draft = TypedDraft();
        var config = ConfigurationLoader.LoadDraft<Typed, Typed>(_ => null, name => draft.GetValueOrDefault(name),
            typed => typed,
            (issue, _) => throw new Xunit.Sdk.XunitException(issue.ToString()),
            issue => throw new Xunit.Sdk.XunitException(issue.ToString()),
            issue => throw new Xunit.Sdk.XunitException(issue.ToString()));
        Assert.Equal(2048, config.Rsa.KeySize);
        Assert.Equal("P-384", config.Ec.Curve);
        Assert.Equal(32, config.Aes!.Length);
        Assert.True(config.Webhook!.Matches(new string('w', 32)));
    }

    [Fact]
    public void An_unparseable_member_is_invalid_with_the_parse_message_and_no_value()
    {
        using var p256 = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var wrongCurve = p256.ExportPkcs8PrivateKeyPem();
        var draft = TypedDraft(ec: wrongCurve);
        var issue = ConfigurationLoader.LoadDraft<Typed, ConfigIssue>(_ => null, name => draft.GetValueOrDefault(name),
            _ => throw new Xunit.Sdk.XunitException("configured"),
            (missing, _) => missing,
            invalid => invalid,
            failure => failure);
        Assert.Equal(ConfigIssueCategory.Invalid, issue.Category);
        Assert.Equal("Typed-Ec", issue.Key);
        Assert.Equal("expected EC P-384 private key; got P-256", issue.Problem);
        Assert.Equal("Typed[Typed-Ec] (Secret) Invalid: expected EC P-384 private key; got P-256", issue.ToString());
        Assert.DoesNotContain("PRIVATE", issue.ToString());
    }

    [Fact]
    public async Task Inspection_reports_an_unparseable_member_invalid_with_the_message()
    {
        var draft = TypedDraft();
        draft["Typed-Webhook"] = "too-short";
        var store = new DictionarySecretStore(draft);
        var rows = await ConfigurationLoader.InspectAsync<Typed>(_ => null, store);
        var webhook = rows.Single(row => row.Member.Property.Name == nameof(Typed.Webhook));
        Assert.Equal(ConfigurationMemberPresence.Invalid, webhook.Presence);
        Assert.Equal("expected a shared secret of at least 32 bytes; got 9", webhook.Problem);
        Assert.True(rows.Where(row => row != webhook).All(row => row.Problem == null));
    }

    private sealed class DictionarySecretStore(IReadOnlyDictionary<string, string> values) : ISecretStore
    {
        public IReadOnlyList<SecretSource> Sources { get; } = new[] { new SecretSource(SecretSource.Draft, "test") };

        public Task<TResult> GetSecretAsync<TResult>(string name, Func<Secret, SecretSource, TResult> onFound,
            Func<TResult> onMissing, Func<string, TResult> onFailure)
            => Task.FromResult(values.TryGetValue(name, out var value) ? onFound(new Secret(value), Sources[0]) : onMissing());
    }

    #endregion

    #region Generators

    private static ConfigurationGenerationContext Context(string member) => new(typeof(Typed),
        ConfigurationMembers.For(typeof(Typed)).Members.Single(candidate => candidate.Property.Name == member),
        new Dictionary<string, string?>());

    private static object Parse(string member, string text)
    {
        var declared = ConfigurationMembers.For(typeof(Typed)).Members.Single(candidate => candidate.Property.Name == member);
        Assert.True(declared.TryConvert(text, out var value, out var problem), problem);
        return value!;
    }

    [Fact]
    public async Task A_generated_rsa_key_is_pkcs8_pem_and_its_id_is_the_thumbprint()
    {
        var result = await ConfigurationGeneration.GenerateAsync(Context(nameof(Typed.Rsa)), r => r, why => throw new Xunit.Sdk.XunitException(why), why => throw new Xunit.Sdk.XunitException(why));
        var pem = result.Values[nameof(Typed.Rsa)];
        Assert.StartsWith("-----BEGIN PRIVATE KEY-----", pem);
        var key = (RsaPrivateKey)Parse(nameof(Typed.Rsa), pem);
        Assert.Equal(key.Thumbprint, result.Values[nameof(Typed.RsaKeyId)]);

        using var jwks = JsonDocument.Parse(result.Outputs.Single(output => output.Label == "JWKS").Value);
        var jwk = jwks.RootElement.GetProperty("keys")[0];
        Assert.Equal(key.Thumbprint, jwk.GetProperty("kid").GetString());
        Assert.Equal("RS256", jwk.GetProperty("alg").GetString());
        Assert.Equal(key.PublicJwk()["n"], jwk.GetProperty("n").GetString());
        Assert.Equal(key.PublicKeyPem, result.Outputs.Single(output => output.Label == "Public key (PEM)").Value);
        Assert.DoesNotContain(result.Outputs, output => output.Label == "Key id");
        Assert.Equal("RSA 2048 key pair (PEM)", ConfigurationMembers.For(typeof(Typed)).Members[0].Generator!.Label);
    }

    [Fact]
    public async Task A_generated_ec_key_is_pkcs8_pem_on_the_declared_curve_with_its_key_id_as_an_output()
    {
        var result = await ConfigurationGeneration.GenerateAsync(Context(nameof(Typed.Ec)), r => r, why => throw new Xunit.Sdk.XunitException(why), why => throw new Xunit.Sdk.XunitException(why));
        var pem = result.Values[nameof(Typed.Ec)];
        Assert.StartsWith("-----BEGIN PRIVATE KEY-----", pem);
        var key = (EcPrivateKey)Parse(nameof(Typed.Ec), pem);
        Assert.Equal("P-384", key.Curve);
        Assert.Equal(key.Thumbprint, result.Outputs.Single(output => output.Label == "Key id").Value);
        Assert.Contains("\"ES384\"", result.Outputs.Single(output => output.Label == "JWKS").Value);
        Assert.Equal("Upload the JWKS.", Assert.Single(result.Instructions).Text);
    }

    [Fact]
    public async Task Generated_symmetric_keys_and_shared_secrets_parse_back_and_draw_from_the_context()
    {
        var random = new CountingRandom();
        var aes = await ConfigurationGeneration.GenerateAsync(Context(nameof(Typed.Aes)) with { Random = random }, r => r, why => throw new Xunit.Sdk.XunitException(why), why => throw new Xunit.Sdk.XunitException(why));
        Assert.Equal(Enumerable.Range(0, 32).Select(i => (byte)i).ToArray(), ((SymmetricKey)Parse(nameof(Typed.Aes), aes.Values[nameof(Typed.Aes)])).ToArray());

        var webhook = await ConfigurationGeneration.GenerateAsync(Context(nameof(Typed.Webhook)), r => r, why => throw new Xunit.Sdk.XunitException(why), why => throw new Xunit.Sdk.XunitException(why));
        var value = webhook.Values[nameof(Typed.Webhook)];
        Assert.Equal(43, value.Length);
        Assert.Matches("^[A-Za-z0-9_-]+$", value);
        Assert.True(((SharedSecret)Parse(nameof(Typed.Webhook), value)).Matches(value));
    }

    private sealed class CountingRandom : RandomNumberGenerator
    {
        private byte next;

        public override void GetBytes(byte[] data)
        {
            for (var i = 0; i < data.Length; i++)
                data[i] = next++;
        }
    }

    [Fact]
    public void Eastfives_key_members_are_typed()
    {
        Assert.Equal(typeof(RsaPrivateKey), typeof(EastFive.Security.Tokens.TokenSigningConfiguration).GetProperty("SigningKey")!.PropertyType);
        Assert.Equal(typeof(RsaPrivateKey), typeof(VoucherConfiguration).GetProperty("SigningKey")!.PropertyType);
        Assert.Equal(typeof(SharedSecret), typeof(ApiKeyConfiguration).GetProperty("ApiKey")!.PropertyType);
        Assert.Equal(typeof(SharedSecret), typeof(SiteAdminAuthorizationConfiguration).GetProperty("Authorization")!.PropertyType);
        Assert.IsType<RsaKeyAttribute>(ConfigurationMembers.For(typeof(EastFive.Security.Tokens.TokenSigningConfiguration))
            .Members.Single(member => member.Property.Name == "SigningKey").Generator);
    }

    [Fact]
    public void A_shared_secret_gate_accepts_only_the_configured_value()
    {
        var value = new string('k', 32);
        var config = new ApiKeyConfiguration { ApiKey = SharedSecret.Parse(value, 32, s => s, why => throw new Xunit.Sdk.XunitException(why)) };
        Assert.True(config.Accepts(value));
        Assert.False(config.Accepts(value.ToUpperInvariant()));
        Assert.False(config.Accepts(null));
    }

    #endregion
}
