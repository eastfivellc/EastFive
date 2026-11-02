using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

using Microsoft.Extensions.Configuration;
using Xunit;

using EastFive.Api.Tests.Harness;
using EastFive.Azure.Auth;
using EastFive.Azure.Persistence.StorageTables;
using EastFive.Azure.Tests.OAuth;
using EastFive.Configuration;
using EastFive.Security;

namespace EastFive.Azure.Tests;

/// <summary>
/// The generators for EastFive.Azure's issued values, each round-tripped through its real consumer:
/// the storage-encryption passphrase and salt through <see cref="StoreEncryptedAttribute"/>'s
/// encrypt/decrypt, and the SAML service-provider key and certificate through the metadata endpoint.
/// </summary>
[Collection(AmbientConfigurationCollection.Name)]
public class AzureGeneratorTests : TestSession
{
    private static ConfigurationGenerationContext Context(Type type, string member, IReadOnlyDictionary<string, string?>? inputs = null)
        => new(type, ConfigurationMembers.For(type).Members.Single(candidate => candidate.Property.Name == member), new Dictionary<string, string?>())
        {
            Inputs = inputs ?? new Dictionary<string, string?>(),
        };

    private static Task<ConfigurationGenerationResult> GenerateAsync(Type type, string member, IReadOnlyDictionary<string, string?>? inputs = null)
        => ConfigurationGeneration.GenerateAsync(Context(type, member, inputs),
            result => result,
            why => throw new Xunit.Sdk.XunitException($"no generator: {why}"),
            why => throw new Xunit.Sdk.XunitException($"refused: {why}"));

    private static TConfig LoadDraft<TConfig>(IReadOnlyDictionary<string, string> values, IReadOnlyDictionary<string, string> secrets)
        where TConfig : IProvideConfiguration, new()
        => ConfigurationLoader.LoadDraft<TConfig, TConfig>(key => values.GetValueOrDefault(key), name => secrets.GetValueOrDefault(name),
            config => config,
            (issue, _) => throw new Xunit.Sdk.XunitException(issue.ToString()),
            issue => throw new Xunit.Sdk.XunitException(issue.ToString()),
            issue => throw new Xunit.Sdk.XunitException(issue.ToString()));

    // ---- storage encryption: passphrase + salt ----

    [Fact]
    public async Task StorageEncryption_GeneratesThePassphraseAndSaltTogether_AndTheyEncryptAndDecrypt()
    {
        var result = await GenerateAsync(typeof(StorageEncryptionConfiguration), nameof(StorageEncryptionConfiguration.EncryptionKey));
        var passphrase = result.Values[nameof(StorageEncryptionConfiguration.EncryptionKey)];
        var salt = result.Values[nameof(StorageEncryptionConfiguration.EncryptionSalt)];
        Assert.Equal(43, passphrase.Length);
        Assert.Matches("^[A-Za-z0-9_-]+$", passphrase);
        Assert.True(Guid.TryParseExact(salt, "D", out _));
        Assert.Contains(result.Instructions, instruction => instruction.Text.Contains("unreadable", StringComparison.Ordinal));

        var loaded = LoadDraft<StorageEncryptionConfiguration>(new Dictionary<string, string>(), new Dictionary<string, string>
        {
            ["StorageEncryption-EncryptionKey"] = passphrase,
            ["StorageEncryption-EncryptionSalt"] = salt,
        });
        var saltBytes = Guid.Parse(loaded.EncryptionSalt.Reveal()).ToByteArray();
        var plain = Encoding.UTF8.GetBytes("stored-at-rest");
        var cipher = StoreEncryptedAttribute.Encrypt2(loaded.EncryptionKey.Reveal(), plain, saltBytes);
        Assert.NotEqual(plain, cipher);
        Assert.Equal(plain, StoreEncryptedAttribute.Decrypt2(loaded.EncryptionKey.Reveal(), cipher, saltBytes));
    }

    // ---- SAML service provider: private key + self-signed certificate ----

    private const string CommonName = "sp.tests.eastfive.invalid";

    [Fact]
    public void SamlServiceProvider_SharesTheCertificateKeyWithSamlConfiguration()
    {
        var provider = ConfigurationMembers.For<SamlServiceProviderConfiguration>().Members
            .Single(member => member.Property.Name == nameof(SamlServiceProviderConfiguration.Certificate));
        Assert.Equal(SamlConfiguration.ServiceProviderCertificateKey, provider.Key);
        Assert.False(ConfigurationMembers.For<SamlConfiguration>().HasSecrets, "SamlConfiguration stays value-only: metadata reads it live");
        var described = ConfigurationCatalog.Describe(typeof(SamlServiceProviderConfiguration)).Members
            .Single(member => member.Property == nameof(SamlServiceProviderConfiguration.PrivateKey));
        Assert.Equal("SamlServiceProvider-PrivateKey", described.SecretNameTemplate);
        Assert.Equal("RSA private key (2048+ bits)", described.ValueKind);
        Assert.Contains(described.GeneratorInputs!, input => input.Name == SamlServiceProviderKeyAttribute.CommonNameInput && input.Optional);
    }

    [Fact]
    public async Task SamlServiceProvider_GeneratesAKeyAndASelfSignedCertificate_ThatMatch()
    {
        var result = await GenerateAsync(typeof(SamlServiceProviderConfiguration), nameof(SamlServiceProviderConfiguration.PrivateKey),
            new Dictionary<string, string?> { [SamlServiceProviderKeyAttribute.CommonNameInput] = CommonName });
        var privateKeyPem = result.Values[nameof(SamlServiceProviderConfiguration.PrivateKey)];
        var certificateBase64 = result.Values[nameof(SamlServiceProviderConfiguration.Certificate)];
        Assert.StartsWith("-----BEGIN PRIVATE KEY-----", privateKeyPem);

        using var certificate = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(certificateBase64));
        Assert.Equal($"CN={CommonName}", certificate.Subject);
        Assert.Equal(certificate.Subject, certificate.Issuer);
        Assert.Equal("sha256RSA", certificate.SignatureAlgorithm.FriendlyName);
        Assert.InRange((certificate.NotAfter - certificate.NotBefore).TotalDays, 729, 732);
        using var publicKey = certificate.GetRSAPublicKey()!;
        using var privateKey = RSA.Create();
        privateKey.ImportFromPem(privateKeyPem);
        Assert.Equal(privateKey.ExportParameters(false).Modulus, publicKey.ExportParameters(false).Modulus);
        Assert.Equal(2048, publicKey.KeySize);

        Assert.StartsWith("-----BEGIN CERTIFICATE-----", result.Outputs.Single(output => output.Label == "Certificate (PEM)").Value);
        Assert.Contains(result.Instructions, instruction => instruction.Text.Contains("saml-metadata", StringComparison.Ordinal));

        // The type loads the pair and validates that they belong together.
        var loaded = LoadDraft<SamlServiceProviderConfiguration>(
            new Dictionary<string, string> { [SamlConfiguration.ServiceProviderCertificateKey] = certificateBase64 },
            new Dictionary<string, string> { ["SamlServiceProvider-PrivateKey"] = privateKeyPem });
        Assert.Equal(2048, loaded.PrivateKey.KeySize);
        var other = RsaPrivateKey.Generate().CreateRsa().ExportPkcs8PrivateKeyPem();
        var mismatch = ConfigurationLoader.LoadDraft<SamlServiceProviderConfiguration, ConfigIssue>(
            key => key == SamlConfiguration.ServiceProviderCertificateKey ? certificateBase64 : null,
            name => name == "SamlServiceProvider-PrivateKey" ? other : null,
            _ => throw new Xunit.Sdk.XunitException("loaded a mismatched pair"), (issue, _) => issue, issue => issue, issue => issue);
        Assert.Equal(ConfigIssueCategory.Invalid, mismatch.Category);
    }

    [Fact]
    public async Task SamlServiceProvider_WithoutACommonName_UsesTheDefault()
    {
        var result = await GenerateAsync(typeof(SamlServiceProviderConfiguration), nameof(SamlServiceProviderConfiguration.PrivateKey));
        using var certificate = X509CertificateLoader.LoadCertificate(
            Convert.FromBase64String(result.Values[nameof(SamlServiceProviderConfiguration.Certificate)]));
        Assert.Equal($"CN={SamlServiceProviderKeyAttribute.DefaultCommonName}", certificate.Subject);
    }

    [Fact]
    public async Task SamlMetadata_PublishesTheGeneratedCertificate_WhosePublicKeyMatchesThePrivateKey()
    {
        var result = await GenerateAsync(typeof(SamlServiceProviderConfiguration), nameof(SamlServiceProviderConfiguration.PrivateKey),
            new Dictionary<string, string?> { [SamlServiceProviderKeyAttribute.CommonNameInput] = CommonName });
        var certificateBase64 = result.Values[nameof(SamlServiceProviderConfiguration.Certificate)];

        TestConfiguration.Ensure();
        Environment.SetEnvironmentVariable(SamlConfiguration.ServiceProviderCertificateKey, certificateBase64);
        Reinitialize();
        try
        {
            var method = typeof(EastFive.Azure.Auth.SAMLMetadata).GetMethod(nameof(EastFive.Azure.Auth.SAMLMetadata.Get))!;
            var capture = await DispatchRawAsync(method, OAuthRequests.Bare(HttpMethod.Get, "/.well-known/saml-metadata",
                query: new Dictionary<string, string> { ["tag"] = "generator-test" }));
            Assert.True(capture.TryGet("onResponse", out var arguments), capture.BranchName);
            var xml = XDocument.Parse((string)arguments[0]!);
            var published = xml.Descendants(XNamespace.Get("http://www.w3.org/2000/09/xmldsig#") + "X509Certificate").Single().Value;
            Assert.Equal(certificateBase64, published);

            using var certificate = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(published));
            using var privateKey = RSA.Create();
            privateKey.ImportFromPem(result.Values[nameof(SamlServiceProviderConfiguration.PrivateKey)]);
            var data = Encoding.UTF8.GetBytes("signed by the service provider");
            var signature = privateKey.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            Assert.True(certificate.GetRSAPublicKey()!.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        }
        finally
        {
            Environment.SetEnvironmentVariable(SamlConfiguration.ServiceProviderCertificateKey, null);
            Reinitialize();
        }
    }

    private static void Reinitialize()
        => EastFive.Web.Configuration.ConfigurationExtensions.Initialize(new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build());
}
