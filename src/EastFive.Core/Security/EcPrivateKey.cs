#nullable enable
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

using EastFive.Configuration;

namespace EastFive.Security
{
    /// <summary>
    /// A configured EC private key on P-256, P-384 or P-521, parsed and validated when the
    /// configuration loads. Accepts SEC1 or PKCS#8 PEM, base64 DER (PKCS#8, then SEC1) and a
    /// private JWK. Declare the member with <see cref="EcKeyAttribute"/>, which names the curve.
    /// </summary>
    /// <remarks>The private scalar is held in a private field. <see cref="Ecdsa"/> is one shared instance for
    /// the key's lifetime; <see cref="CreateEcdsa"/> hands out a fresh one the caller owns.</remarks>
    public sealed class EcPrivateKey
    {
        private readonly ECParameters parameters;
        private readonly Lazy<ECDsa> shared;

        private EcPrivateKey(ECParameters parameters, ConfigurationEcCurve curve)
        {
            this.parameters = parameters;
            shared = new Lazy<ECDsa>(CreateEcdsa);
            CurveValue = curve;
            Thumbprint = JsonWebKeyThumbprint.Ec(Curve, KeyText.Base64Url(parameters.Q.X!), KeyText.Base64Url(parameters.Q.Y!));
        }

        private ConfigurationEcCurve CurveValue { get; }

        /// <summary>The JWK curve name: P-256, P-384 or P-521.</summary>
        public string Curve => CurveName(CurveValue);

        /// <summary>The JWS <c>alg</c> the curve implies: ES256, ES384 or ES512 (P-521 is ES512, not ES521).</summary>
        public string Algorithm => CurveValue switch
        {
            ConfigurationEcCurve.P384 => "ES384",
            ConfigurationEcCurve.P521 => "ES512",
            _ => "ES256",
        };

        /// <summary>The RFC 7638 thumbprint (base64url): the default key id where we control registration.</summary>
        public string Thumbprint { get; }

        public string PublicKeyPem
        {
            get
            {
                using var ecdsa = CreateEcdsa();
                return ecdsa.ExportSubjectPublicKeyInfoPem();
            }
        }

        /// <summary>The key's shared ECDsa instance. Never dispose it: it lives as long as the key, and
        /// signature-provider caches (IdentityModel's) hold on to it.</summary>
        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public ECDsa Ecdsa => shared.Value;

        /// <summary>A new ECDsa instance holding the key; the caller disposes it.</summary>
        public ECDsa CreateEcdsa() => ECDsa.Create(parameters);

        /// <summary>The public JWK: kty, use, alg, kid (the <see cref="Thumbprint"/> unless given), crv, x, y; base64url.</summary>
        public IReadOnlyDictionary<string, string> PublicJwk(string? keyId = null, string use = "sig")
            => new Dictionary<string, string>
            {
                ["kty"] = "EC",
                ["use"] = use,
                ["alg"] = Algorithm,
                ["kid"] = keyId ?? Thumbprint,
                ["crv"] = Curve,
                ["x"] = KeyText.Base64Url(parameters.Q.X!),
                ["y"] = KeyText.Base64Url(parameters.Q.Y!),
            };

        public override string ToString() => $"EC {Curve} private key";

        internal string ExportPkcs8Pem()
        {
            using var ecdsa = CreateEcdsa();
            return ecdsa.ExportPkcs8PrivateKeyPem();
        }

        public static string CurveName(ConfigurationEcCurve curve) => curve switch
        {
            ConfigurationEcCurve.P384 => "P-384",
            ConfigurationEcCurve.P521 => "P-521",
            _ => "P-256",
        };

        private static ECCurve NamedCurve(ConfigurationEcCurve curve) => curve switch
        {
            ConfigurationEcCurve.P384 => ECCurve.NamedCurves.nistP384,
            ConfigurationEcCurve.P521 => ECCurve.NamedCurves.nistP521,
            _ => ECCurve.NamedCurves.nistP256,
        };

        /// <summary>A new key: what a member's generator writes, as PKCS#8 PEM.</summary>
        public static EcPrivateKey Generate(ConfigurationEcCurve curve)
        {
            using var ecdsa = ECDsa.Create(NamedCurve(curve));
            return new EcPrivateKey(ecdsa.ExportParameters(true), curve);
        }

        /// <summary>
        /// Text to a key on <paramref name="curve"/> (any supported curve when null), or the reason it
        /// is not one: "expected EC P-384 private key; got P-256", "public key only", "got RSA 2048
        /// private key". The reason never contains the text.
        /// </summary>
        public static TResult Parse<TResult>(string text,
            Func<EcPrivateKey, TResult> onParsed,
            Func<string, TResult> onInvalid,
            ConfigurationEcCurve? curve = null)
        {
            var expected = curve is { } required ? $"EC {CurveName(required)} private key" : "EC private key";
            var (outcome, read, refusal) = Read(text);
            switch (outcome)
            {
                case KeyReadOutcome.PublicOnly:
                    return onInvalid($"expected {expected}; got public key only");
                case KeyReadOutcome.Refused:
                    return onInvalid($"expected {expected}; {refusal}");
                case KeyReadOutcome.NotThisKind:
                    return onInvalid(RsaPrivateKey.Identify(text) is { } other
                        ? $"expected {expected}; got {other}"
                        : $"expected {expected} (PEM, base64 DER or private JWK); the value is none of these");
            }
            if (CurveOf(read.Curve) is not { } found)
                return onInvalid($"expected {expected}; got an unsupported curve");
            if (curve is { } wanted && wanted != found)
                return onInvalid($"expected {expected}; got {CurveName(found)}");
            return onParsed(new EcPrivateKey(read, found));
        }

        /// <summary>"EC P-256 private key" when the text is one, else null; for another kind's message.</summary>
        internal static string? Identify(string text)
            => Read(text) is (KeyReadOutcome.Private, var read, _)
                ? CurveOf(read.Curve) is { } curve ? $"EC {CurveName(curve)} private key" : "EC private key"
                : null;

        private static ConfigurationEcCurve? CurveOf(ECCurve curve)
            => (curve.Oid?.Value, curve.Oid?.FriendlyName) switch
            {
                ("1.2.840.10045.3.1.7", _) or (_, "nistP256" or "ECDSA_P256" or "secp256r1" or "prime256v1") => ConfigurationEcCurve.P256,
                ("1.3.132.0.34", _) or (_, "nistP384" or "ECDSA_P384" or "secp384r1") => ConfigurationEcCurve.P384,
                ("1.3.132.0.35", _) or (_, "nistP521" or "ECDSA_P521" or "secp521r1") => ConfigurationEcCurve.P521,
                _ => null,
            };

        private static ConfigurationEcCurve? CurveOf(string? crv) => crv switch
        {
            "P-256" => ConfigurationEcCurve.P256,
            "P-384" => ConfigurationEcCurve.P384,
            "P-521" => ConfigurationEcCurve.P521,
            _ => null,
        };

        private static (KeyReadOutcome, ECParameters, string?) Read(string text)
        {
            text = text.Trim();
            using var ecdsa = ECDsa.Create();

            (KeyReadOutcome, ECParameters, string?) Private()
                => KeyText.Try(() => ecdsa.ExportParameters(true))
                    ? (KeyReadOutcome.Private, ecdsa.ExportParameters(true), null)
                    : (KeyReadOutcome.PublicOnly, default, null);
            (KeyReadOutcome, ECParameters, string?) Outcome(KeyReadOutcome outcome, string? refusal = null) => (outcome, default, refusal);

            if (KeyText.IsPem(text))
            {
                var pem = KeyText.NormalizePem(text);
                var label = KeyText.PemLabel(pem);
                if (label != null && label.Contains("ENCRYPTED", StringComparison.Ordinal))
                    return Outcome(KeyReadOutcome.Refused, "encrypted PEM is not supported");
                return KeyText.Try(() => ecdsa.ImportFromPem(pem)) ? Private() : Outcome(KeyReadOutcome.NotThisKind);
            }

            if (KeyText.TryFromBase64(text, out var bytes))
            {
                if (KeyText.IsRsaXml(bytes))
                    return Outcome(KeyReadOutcome.NotThisKind);
                if (KeyText.Try(() => { ecdsa.ImportPkcs8PrivateKey(bytes, out var read); Whole(read, bytes); })
                        || KeyText.Try(() => { ecdsa.ImportECPrivateKey(bytes, out var read); Whole(read, bytes); }))
                    return Private();
                if (KeyText.Try(() => { ecdsa.ImportSubjectPublicKeyInfo(bytes, out var read); Whole(read, bytes); }))
                    return Outcome(KeyReadOutcome.PublicOnly);
                return Outcome(KeyReadOutcome.NotThisKind);
            }

            if (KeyText.JsonObject(text) is { } jwk)
            {
                if (KeyText.Member(jwk, "kty") != "EC")
                    return Outcome(KeyReadOutcome.NotThisKind);
                if (CurveOf(KeyText.Member(jwk, "crv")) is not { } curve)
                    return Outcome(KeyReadOutcome.Refused, "the JWK names an unsupported curve");
                var x = KeyText.Base64UrlMember(jwk, "x");
                var y = KeyText.Base64UrlMember(jwk, "y");
                if (x == null || y == null)
                    return Outcome(KeyReadOutcome.Refused, "the JWK has no x or y");
                if (KeyText.Base64UrlMember(jwk, "d") is not { } d)
                    return Outcome(KeyReadOutcome.PublicOnly);
                var jwkParameters = new ECParameters { Curve = NamedCurve(curve), Q = new ECPoint { X = x, Y = y }, D = d };
                return KeyText.Try(() => ecdsa.ImportParameters(jwkParameters))
                    ? Private()
                    : Outcome(KeyReadOutcome.Refused, "the JWK's parameters do not form a key");
            }

            return Outcome(KeyReadOutcome.NotThisKind);
        }

        /// <summary>DER with trailing bytes is not the encoding it parsed as.</summary>
        private static void Whole(int read, byte[] bytes)
        {
            if (read != bytes.Length)
                throw new CryptographicException("Trailing data.");
        }
    }
}
