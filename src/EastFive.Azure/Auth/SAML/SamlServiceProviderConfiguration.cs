#nullable enable
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using EastFive.Configuration;
using EastFive.Security;

namespace EastFive.Azure.Auth
{
    /// <summary>
    /// The SAML service provider's own key pair: the private key (secret <c>SamlServiceProvider-PrivateKey</c>)
    /// and the self-signed certificate published in the metadata, under the SAME values key
    /// <see cref="SamlConfiguration"/> reads (<see cref="SamlConfiguration.ServiceProviderCertificateKey"/>).
    /// </summary>
    /// <remarks>
    /// A type of its own, not a member of <see cref="SamlConfiguration"/>: a secret member would make that
    /// value-only type load from the host's startup set, so a host without the key (or one that never loads
    /// the type) would publish metadata with no certificate and no NameID format. Nothing signs with the key
    /// yet; it is kept so the certificate can be renewed, and used once requests are signed. Two types
    /// sharing one key is the same arrangement as the channel host's engine secret.
    /// </remarks>
    [ConfigurationType("SamlServiceProvider", Label = "SAML service-provider key",
        Description = "The SAML service provider's private key and the self-signed certificate its metadata publishes.",
        Scope = ConfigurationScope.Environment)]
    public sealed class SamlServiceProviderConfiguration : IProvideConfiguration
    {
        [ConfigurationMember(SamlConfiguration.ServiceProviderCertificateKey, Kind = ConfigurationMemberKind.Optional,
            Label = "Service-provider certificate", Description = "Public signing certificate (base64 DER) published in the metadata. Public, not a secret.")]
        public string? Certificate { get; set; }

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "Private key",
            Description = "The RSA private key the certificate was issued for. Generate both together.")]
        [SamlServiceProviderKey]
        public RsaPrivateKey PrivateKey { get; set; } = default!;

        /// <summary>A configured certificate must belong to the private key.</summary>
        public string? Validate()
        {
            if (PrivateKey == null || string.IsNullOrWhiteSpace(Certificate))
                return null;
            try
            {
                using var certificate = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(Certificate.Trim()));
                using var published = certificate.GetRSAPublicKey();
                return published != null
                    && published.ExportParameters(false).Modulus.AsSpan().SequenceEqual(PrivateKey.Rsa.ExportParameters(false).Modulus)
                    ? null
                    : SamlConfiguration.ServiceProviderCertificateKey;
            }
            catch (Exception ex) when (ex is FormatException or CryptographicException)
            {
                return SamlConfiguration.ServiceProviderCertificateKey;
            }
        }
    }

    /// <summary>
    /// Generates the SAML service provider's RSA key and a self-signed X.509 certificate for it together:
    /// SHA-256, two years, a random serial, CN from the optional <see cref="CommonNameInput"/>. Fills the
    /// private key (PKCS#8 PEM) and the certificate (base64 DER, as the metadata reads it).
    /// </summary>
    public sealed class SamlServiceProviderKeyAttribute : RsaKeyAttribute
    {
        public const string CommonNameInput = "commonName";
        public const string DefaultCommonName = "EastFive SAML service provider";
        public static readonly TimeSpan Validity = TimeSpan.FromDays(730);

        public const string CertificateInstruction =
            "The SP metadata (/.well-known/saml-metadata?tag={tag}) publishes this certificate once it is saved and the "
            + "host restarts. Update the service-provider registration at the IdP for EVERY SAML tag: re-import the "
            + "metadata, or upload the certificate. Keep the private key: the certificate is renewed from it.";

        public SamlServiceProviderKeyAttribute()
        {
            Instruction = CertificateInstruction;
        }

        public override string Label => "RSA 2048 key with a self-signed certificate (2 years)";

        public override IReadOnlyList<ConfigurationGenerationInput> Inputs { get; } = new[]
        {
            new ConfigurationGenerationInput(CommonNameInput, "Common name", Optional: true,
                Description: "The certificate's CN, e.g. the site's host name. \"" + DefaultCommonName + "\" when blank."),
        };

        protected override ConfigurationGenerationResult Generate(ConfigurationGenerationContext context, Lazy<string> keyId)
        {
            var key = RsaPrivateKey.Generate(MinBits, Algorithm);
            var commonName = context.InputValue(CommonNameInput)?.Trim() ?? DefaultCommonName;
            var request = new CertificateRequest(new X500DistinguishedName($"CN={EscapeCommonName(commonName)}"),
                key.Rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true));
            request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));
            var now = context.Clock.GetUtcNow();
            using var certificate = request.Create(new X500DistinguishedName($"CN={EscapeCommonName(commonName)}"),
                X509SignatureGenerator.CreateForRSA(key.Rsa, RSASignaturePadding.Pkcs1),
                now.AddMinutes(-5), now.Add(Validity), Serial(context));
            var der = certificate.Export(X509ContentType.Cert);
            return ConfigurationGenerationResult.Of(context.Member.Property.Name, ExportPem(key))
                .WithValue(nameof(SamlServiceProviderConfiguration.Certificate), Convert.ToBase64String(der))
                .WithOutput("Certificate (PEM)", PemEncoding.WriteString("CERTIFICATE", der), ConfigurationOutputKind.Pem)
                .WithOutput("SHA-256 fingerprint", certificate.GetCertHashString(HashAlgorithmName.SHA256));
        }

        private static string ExportPem(RsaPrivateKey key)
        {
            using var rsa = key.CreateRsa();
            return rsa.ExportPkcs8PrivateKeyPem();
        }

        /// <summary>A positive 16-byte serial from the context's random source.</summary>
        private static byte[] Serial(ConfigurationGenerationContext context)
        {
            var serial = context.RandomBytes(16);
            serial[0] &= 0x7F;
            serial[0] |= 0x01;
            return serial;
        }

        /// <summary>The CN as an RDN value: quoted when it carries a separator.</summary>
        private static string EscapeCommonName(string commonName)
            => commonName.IndexOfAny(new[] { ',', '+', '"', '\\', '<', '>', ';', '=' }) >= 0
                ? "\"" + commonName.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""
                : commonName;
    }
}
