#nullable enable
using System;
using System.Collections.Generic;

using EastFive.Configuration;

namespace EastFive.Security
{
    /// <summary>
    /// The base of the typed key value attributes: each declares a member's constraints, parses its
    /// text (<see cref="IParseConfigurationValue"/>) and is its natural generator. Key material
    /// always: the member must be a secret.
    /// </summary>
    public abstract class KeyValueAttribute : ConfigurationValueGeneratorAttribute, IParseConfigurationValue
    {
        public abstract Type ValueType { get; }

        public abstract string ValueKind { get; }

        public abstract IReadOnlyList<string> AcceptedFormats { get; }

        public bool IsSecretMaterial => true;

        public abstract TResult Parse<TResult>(string text, Func<object, TResult> onParsed, Func<string, TResult> onInvalid);

        /// <summary>A key pair's draft: the private key (PKCS#8 PEM) fills the member, its thumbprint fills
        /// <see cref="ConfigurationValueGeneratorAttribute.KeyIdMember"/> or is offered as the key id output.</summary>
        protected ConfigurationGenerationResult KeyPair(ConfigurationGenerationContext context,
            string privateKeyPem, string publicKeyPem, IReadOnlyDictionary<string, string> publicJwk, string thumbprint)
        {
            var result = ConfigurationGenerationResult.Of(context.Member.Property.Name, privateKeyPem);
            if (KeyIdMember != null)
                result = result.WithValue(KeyIdMember, thumbprint);
            return WithKeyPairOutputs(result, publicKeyPem, publicJwk, thumbprint);
        }
    }

    /// <summary>
    /// An <see cref="RsaPrivateKey"/> member: at least <see cref="MinBits"/> bits, signing with
    /// <see cref="Algorithm"/>. Generates a <see cref="MinBits"/>-bit key as PKCS#8 PEM, with the
    /// public key, JWKS and thumbprint key id as outputs.
    /// </summary>
    public sealed class RsaKeyAttribute : KeyValueAttribute
    {
        private readonly string algorithm = RsaPrivateKey.DefaultAlgorithm;

        public RsaKeyAttribute(int minBits = 2048)
        {
            if (minBits < 2048 || minBits > 16384 || minBits % 8 != 0)
                throw new ArgumentOutOfRangeException(nameof(minBits), minBits, "An RSA key is 2048 to 16384 bits, in whole bytes.");
            MinBits = minBits;
        }

        public int MinBits { get; }

        /// <summary>The JWS <c>alg</c> (RS256, RS384, RS512, PS256, PS384 or PS512).</summary>
        public string Algorithm
        {
            get => algorithm;
            init => algorithm = RsaPrivateKey.IsAlgorithm(value)
                ? value
                : throw new ArgumentException($"'{value}' is not an RSA JWS algorithm.", nameof(Algorithm));
        }

        public override Type ValueType => typeof(RsaPrivateKey);

        public override string ValueKind => $"RSA private key ({MinBits}+ bits)";

        public override IReadOnlyList<string> AcceptedFormats { get; } = new[]
        {
            "PEM (PKCS#8 or PKCS#1)", "base64 DER (PKCS#8 or PKCS#1)", "base64 .NET RSA XML", "JWK (private)",
        };

        public override string Label => $"RSA {MinBits} key pair (PEM)";

        public override TResult Parse<TResult>(string text, Func<object, TResult> onParsed, Func<string, TResult> onInvalid)
            => RsaPrivateKey.Parse(text, key => onParsed(key), onInvalid, MinBits, Algorithm);

        protected override ConfigurationGenerationResult Generate(ConfigurationGenerationContext context, Lazy<string> keyId)
        {
            var key = RsaPrivateKey.Generate(MinBits, Algorithm);
            return KeyPair(context, key.ExportPkcs8Pem(), key.PublicKeyPem, key.PublicJwk(), key.Thumbprint);
        }
    }

    /// <summary>
    /// An <see cref="EcPrivateKey"/> member on <see cref="Curve"/>. Generates a key on that curve as
    /// PKCS#8 PEM, with the public key, JWKS and thumbprint key id as outputs.
    /// </summary>
    public sealed class EcKeyAttribute : KeyValueAttribute
    {
        public EcKeyAttribute(ConfigurationEcCurve curve) => Curve = curve;

        public ConfigurationEcCurve Curve { get; }

        public override Type ValueType => typeof(EcPrivateKey);

        public override string ValueKind => $"EC {EcPrivateKey.CurveName(Curve)} private key";

        public override IReadOnlyList<string> AcceptedFormats { get; } = new[]
        {
            "PEM (PKCS#8 or SEC1)", "base64 DER (PKCS#8 or SEC1)", "JWK (private)",
        };

        public override string Label => $"EC {EcPrivateKey.CurveName(Curve)} key pair (PEM)";

        public override TResult Parse<TResult>(string text, Func<object, TResult> onParsed, Func<string, TResult> onInvalid)
            => EcPrivateKey.Parse(text, key => onParsed(key), onInvalid, Curve);

        protected override ConfigurationGenerationResult Generate(ConfigurationGenerationContext context, Lazy<string> keyId)
        {
            var key = EcPrivateKey.Generate(Curve);
            return KeyPair(context, key.ExportPkcs8Pem(), key.PublicKeyPem, key.PublicJwk(), key.Thumbprint);
        }
    }

    /// <summary>
    /// A <see cref="SymmetricKey"/> member of exactly <see cref="Length"/> bytes in <see cref="Encoding"/>
    /// (default base64). Generates that many random bytes in that encoding.
    /// </summary>
    public sealed class SymmetricKeyAttribute : KeyValueAttribute
    {
        public SymmetricKeyAttribute(int length)
        {
            if (length is < 1 or > 1024)
                throw new ArgumentOutOfRangeException(nameof(length), length, "A symmetric key is 1 to 1024 bytes.");
            Length = length;
        }

        public int Length { get; }

        public ConfigurationRandomEncoding Encoding { get; init; } = ConfigurationRandomEncoding.Base64;

        public override Type ValueType => typeof(SymmetricKey);

        public override string ValueKind => $"{Length}-byte symmetric key";

        public override IReadOnlyList<string> AcceptedFormats => new[] { SymmetricKey.EncodingName(Encoding) };

        public override string Label => $"Random {Length}-byte key, {SymmetricKey.EncodingName(Encoding)}";

        public override TResult Parse<TResult>(string text, Func<object, TResult> onParsed, Func<string, TResult> onInvalid)
            => SymmetricKey.Parse(text, Length, Encoding, key => onParsed(key), onInvalid);

        protected override ConfigurationGenerationResult Generate(ConfigurationGenerationContext context, Lazy<string> keyId)
            => ConfigurationGenerationResult.Of(context.Member.Property.Name, Encode(context.RandomBytes(Length), Encoding));
    }

    /// <summary>
    /// A <see cref="SharedSecret"/> member of at least <see cref="MinLength"/> UTF-8 bytes (default 32).
    /// Generates 32 random bytes (more if the minimum needs them), base64url: safe in a URL and a header.
    /// </summary>
    public sealed class SharedSecretAttribute : KeyValueAttribute
    {
        private readonly int minLength = 32;

        public int MinLength
        {
            get => minLength;
            init => minLength = value >= 1
                ? value
                : throw new ArgumentOutOfRangeException(nameof(MinLength), value, "A shared secret's minimum is at least 1 byte.");
        }

        private int GeneratedBytes => Math.Max(32, MinLength);

        public override Type ValueType => typeof(SharedSecret);

        public override string ValueKind => $"Shared secret ({MinLength}+ bytes)";

        public override IReadOnlyList<string> AcceptedFormats { get; } = new[] { "any text (UTF-8)" };

        public override string Label => $"Random {GeneratedBytes} bytes, base64url";

        public override TResult Parse<TResult>(string text, Func<object, TResult> onParsed, Func<string, TResult> onInvalid)
            => SharedSecret.Parse(text, MinLength, secret => onParsed(secret), onInvalid);

        protected override ConfigurationGenerationResult Generate(ConfigurationGenerationContext context, Lazy<string> keyId)
            => ConfigurationGenerationResult.Of(context.Member.Property.Name, Base64Url(context.RandomBytes(GeneratedBytes)));
    }
}
