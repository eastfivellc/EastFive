#nullable enable
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace EastFive.Security
{
    /// <summary>
    /// A configured RSA private key, parsed and validated when the configuration loads. Accepts
    /// PKCS#1 or PKCS#8 PEM, base64 DER (PKCS#8, then PKCS#1), base64 of the .NET RSA XML, and a
    /// private JWK. Declare the member with <see cref="RsaKeyAttribute"/>.
    /// </summary>
    /// <remarks>
    /// The private parameters are held in a private field: serializers, interpolation and logging
    /// see only the public facts. <see cref="Rsa"/> is one shared instance for the key's lifetime
    /// (what token libraries cache signature providers against); <see cref="CreateRsa"/> hands out
    /// a fresh one the caller owns.
    /// </remarks>
    public sealed class RsaPrivateKey
    {
        public const string DefaultAlgorithm = "RS256";

        /// <summary>The JWS algorithms an RSA key signs with: what an algorithm choice list for an RSA
        /// key offers (never ES*, which needs an EC key).</summary>
        public static IReadOnlyList<string> SupportedAlgorithms { get; } = new[]
        {
            "RS256", "RS384", "RS512", "PS256", "PS384", "PS512",
        };

        private static readonly HashSet<string> Algorithms = new(SupportedAlgorithms, StringComparer.Ordinal);

        private readonly RSAParameters parameters;
        private readonly Lazy<System.Security.Cryptography.RSA> shared;

        private RsaPrivateKey(RSAParameters parameters, string algorithm)
        {
            this.parameters = parameters;
            shared = new Lazy<System.Security.Cryptography.RSA>(CreateRsa);
            Algorithm = algorithm;
            var publicJwk = PublicMembers(parameters);
            Thumbprint = JsonWebKeyThumbprint.Rsa(publicJwk.n, publicJwk.e);
        }

        public int KeySize => parameters.Modulus!.Length * 8;

        /// <summary>The JWS <c>alg</c> this key signs with (RS256 unless the member declares otherwise).</summary>
        public string Algorithm { get; }

        /// <summary>The RFC 7638 thumbprint (base64url): the default key id where we control registration.</summary>
        public string Thumbprint { get; }

        public string PublicKeyPem
        {
            get
            {
                using var rsa = CreateRsa();
                return rsa.ExportSubjectPublicKeyInfoPem();
            }
        }

        /// <summary>The key's shared RSA instance. Never dispose it: it lives as long as the key, and
        /// signature-provider caches (IdentityModel's) hold on to it.</summary>
        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public System.Security.Cryptography.RSA Rsa => shared.Value;

        /// <summary>A new RSA instance holding the key; the caller disposes it.</summary>
        public System.Security.Cryptography.RSA CreateRsa() => System.Security.Cryptography.RSA.Create(parameters);

        /// <summary>The public JWK: kty, use, alg, kid (the <see cref="Thumbprint"/> unless given), n, e; base64url.</summary>
        public IReadOnlyDictionary<string, string> PublicJwk(string? keyId = null, string use = "sig")
        {
            var (n, e) = PublicMembers(parameters);
            return new Dictionary<string, string>
            {
                ["kty"] = "RSA",
                ["use"] = use,
                ["alg"] = Algorithm,
                ["kid"] = keyId ?? Thumbprint,
                ["n"] = n,
                ["e"] = e,
            };
        }

        public override string ToString() => $"RSA {KeySize} private key";

        internal string ExportPkcs8Pem()
        {
            using var rsa = CreateRsa();
            return rsa.ExportPkcs8PrivateKeyPem();
        }

        public static bool IsAlgorithm(string algorithm) => Algorithms.Contains(algorithm);

        /// <summary>A new key: what a member's generator writes, as PKCS#8 PEM.</summary>
        public static RsaPrivateKey Generate(int bits = 2048, string algorithm = DefaultAlgorithm)
        {
            using var rsa = System.Security.Cryptography.RSA.Create(bits);
            return new RsaPrivateKey(rsa.ExportParameters(true), algorithm);
        }

        /// <summary>
        /// Text to a key, or the reason it is not one: "RSA 1024 &lt; 2048", "public key only",
        /// "expected RSA private key; got EC P-256 private key". The reason never contains the text.
        /// </summary>
        public static TResult Parse<TResult>(string text,
            Func<RsaPrivateKey, TResult> onParsed,
            Func<string, TResult> onInvalid,
            int minBits = 2048,
            string algorithm = DefaultAlgorithm)
        {
            var (outcome, read, refusal) = Read(text);
            switch (outcome)
            {
                case KeyReadOutcome.PublicOnly:
                    return onInvalid("expected RSA private key; got public key only");
                case KeyReadOutcome.Refused:
                    return onInvalid($"expected RSA private key; {refusal}");
                case KeyReadOutcome.NotThisKind:
                    return onInvalid(EcPrivateKey.Identify(text) is { } other
                        ? $"expected RSA private key; got {other}"
                        : "expected RSA private key (PEM, base64 DER, base64 .NET RSA XML or private JWK); the value is none of these");
            }
            var key = new RsaPrivateKey(read, algorithm);
            return key.KeySize < minBits
                ? onInvalid($"RSA {key.KeySize} < {minBits}")
                : onParsed(key);
        }

        /// <summary>"RSA 2048 private key" when the text is one (any size), else null; for another kind's message.</summary>
        internal static string? Identify(string text)
            => Read(text) is (KeyReadOutcome.Private, var read, _) ? $"RSA {read.Modulus!.Length * 8} private key" : null;

        private static (string n, string e) PublicMembers(RSAParameters parameters)
            => (KeyText.Base64Url(parameters.Modulus!), KeyText.Base64Url(parameters.Exponent!));

        private static (KeyReadOutcome, RSAParameters, string?) Read(string text)
        {
            text = text.Trim();
            using var rsa = System.Security.Cryptography.RSA.Create();

            (KeyReadOutcome, RSAParameters, string?) Private()
                => KeyText.Try(() => rsa.ExportParameters(true))
                    ? (KeyReadOutcome.Private, rsa.ExportParameters(true), null)
                    : (KeyReadOutcome.PublicOnly, default, null);
            (KeyReadOutcome, RSAParameters, string?) Outcome(KeyReadOutcome outcome, string? refusal = null) => (outcome, default, refusal);

            if (KeyText.IsPem(text))
            {
                var pem = KeyText.NormalizePem(text);
                var label = KeyText.PemLabel(pem);
                if (label != null && label.Contains("ENCRYPTED", StringComparison.Ordinal))
                    return Outcome(KeyReadOutcome.Refused, "encrypted PEM is not supported");
                return KeyText.Try(() => rsa.ImportFromPem(pem)) ? Private() : Outcome(KeyReadOutcome.NotThisKind);
            }

            if (KeyText.TryFromBase64(text, out var bytes))
            {
                if (KeyText.IsRsaXml(bytes))
                    return KeyText.Try(() => rsa.FromXmlString(KeyText.Xml(bytes))) ? Private() : Outcome(KeyReadOutcome.NotThisKind);
                if (KeyText.Try(() => { rsa.ImportPkcs8PrivateKey(bytes, out var read); Whole(read, bytes); })
                        || KeyText.Try(() => { rsa.ImportRSAPrivateKey(bytes, out var read); Whole(read, bytes); }))
                    return Private();
                if (KeyText.Try(() => { rsa.ImportSubjectPublicKeyInfo(bytes, out var read); Whole(read, bytes); })
                        || KeyText.Try(() => { rsa.ImportRSAPublicKey(bytes, out var read); Whole(read, bytes); }))
                    return Outcome(KeyReadOutcome.PublicOnly);
                return Outcome(KeyReadOutcome.NotThisKind);
            }

            if (KeyText.JsonObject(text) is { } jwk)
            {
                if (KeyText.Member(jwk, "kty") != "RSA")
                    return Outcome(KeyReadOutcome.NotThisKind);
                var n = KeyText.Base64UrlMember(jwk, "n");
                var e = KeyText.Base64UrlMember(jwk, "e");
                if (n == null || e == null)
                    return Outcome(KeyReadOutcome.Refused, "the JWK has no n or e");
                if (KeyText.Member(jwk, "d") == null)
                    return Outcome(KeyReadOutcome.PublicOnly);
                var jwkParameters = new RSAParameters
                {
                    Modulus = n,
                    Exponent = e,
                    D = KeyText.Base64UrlMember(jwk, "d"),
                    P = KeyText.Base64UrlMember(jwk, "p"),
                    Q = KeyText.Base64UrlMember(jwk, "q"),
                    DP = KeyText.Base64UrlMember(jwk, "dp"),
                    DQ = KeyText.Base64UrlMember(jwk, "dq"),
                    InverseQ = KeyText.Base64UrlMember(jwk, "qi"),
                };
                if (jwkParameters.D == null || jwkParameters.P == null || jwkParameters.Q == null
                        || jwkParameters.DP == null || jwkParameters.DQ == null || jwkParameters.InverseQ == null)
                    return Outcome(KeyReadOutcome.Refused, "the JWK lacks its CRT parameters (p, q, dp, dq, qi)");
                return KeyText.Try(() => rsa.ImportParameters(jwkParameters))
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
