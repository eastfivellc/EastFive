using System;
using System.Linq;
using System.Threading.Tasks;

using Xunit;

using EastFive.Api.Tests;
using EastFive.Configuration;
using EastFive.Web;

namespace EastFive.Azure.Tests;

/// <summary>
/// The secret-read guard over EastFive.Azure and EastFive.Sheets (Core and Api are scanned by
/// EastFive.Api.Tests), plus the catalog of every framework configuration type: every secret is
/// keyless and generated, and the full set of generated names is pinned so a rename is a
/// deliberate, reviewed change (it is a vault migration).
/// </summary>
public class SecretGuardTests
{
    private static readonly string[] ScannedProjects = { "Azure", "Sheets" };

    private static System.Reflection.Assembly[] Framework => new[]
    {
        typeof(Secret).Assembly,
        typeof(EastFive.Api.ApiKeyConfiguration).Assembly,
        typeof(EastFive.Azure.Persistence.StorageAccount).Assembly,
    };

    [Fact]
    public void NoAzureOrSheetsCode_ReadsASecretFromTheValuesStore()
        => SecretReadGuard.AssertNoSecretReads(ScannedProjects, Framework);

    [Fact]
    public void RetiredSecretKeys_AppearNowhereInAzureOrSheets()
        => SecretReadGuard.AssertRetiredKeysAbsent(ScannedProjects);

    [Fact]
    public void AzureAndSheets_MakeSecretsOnlyInTheLoaderAndStores()
        => SecretReadGuard.AssertSecretsMadeOnlyByLoader(ScannedProjects);

    [Fact]
    public void NoFrameworkConstant_HoldsARetiredSecretKey()
    {
        var offences = Framework
            .SelectMany(assembly => assembly.GetTypes())
            .SelectMany(type => type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Where(field => field.IsLiteral && field.FieldType == typeof(string))
                .Select(field => (where: $"{type.FullName}.{field.Name}", value: (string)field.GetRawConstantValue()!)))
            .Where(constant => SecretReadGuard.RetiredSecretKeys.Any(retired => constant.value.StartsWith(retired, StringComparison.Ordinal)))
            .Select(constant => $"{constant.where} = {constant.value}")
            .ToArray();
        Assert.True(offences.Length == 0, string.Join(Environment.NewLine, offences));
    }

    /// <summary>Changing this list renames a vault secret: run the doctor's --vault-migration.</summary>
    private static readonly string[] FrameworkSecretNames =
    {
        "ApplicationInsights-{account}-ApiKey",
        "AzureADB2C-ClientSecret",
        "AzureClientApplication-{account}-ClientSecret",
        "AzureCommunications-ConnectionString",
        "AzureSearch-AdminApiKey",
        "AzureStorage-{account}-ConnectionString",
        "ComputerVision-SubscriptionKey",
        "EastFiveAccessTokens-SigningSecret",
        "EastFiveAdminLogin-SigningKey",
        "EastFiveApi-ApiKey",
        "EastFiveLogin-ClientSecret",
        "EastFiveSiteAdmin-Authorization",
        "EastFiveTokens-SigningKey",
        "EastFiveVoucher-SigningKey",
        "Google-{account}-ClientSecret",
        "Microsoft-{account}-ClientSecret",
        "PingAthena-RestApiKey",
        "Postman-ApiKey",
        "Salesforce-ConsumerSecret",
        "ServiceBus-ConnectionString",
        "StorageDiagnostics-TableInformationToken",
        "StorageEncryption-EncryptionKey",
        "StorageEncryption-EncryptionSalt",
        "TeamsNotifications-WebhookUrl",
    };

    [Fact]
    public void EveryFrameworkSecret_IsKeylessAndGenerated_AndTheNamesArePinned()
    {
        var catalog = ConfigurationCatalog.For(Framework);
        Assert.All(catalog.Where(type => type.Members.Any(member => member.Kind == ConfigurationMemberKind.Secret)),
            type => Assert.True(type.GeneratesSecretNames, $"{type.Type.Name} names its own secrets"));
        Assert.All(catalog.SelectMany(type => type.Members).Where(member => member.Kind == ConfigurationMemberKind.Secret),
            member => Assert.Null(member.Key));
        Assert.Equal(FrameworkSecretNames,
            catalog.SelectMany(type => type.SecretNames()).Distinct().OrderBy(name => name, StringComparer.Ordinal));
    }

    /// <summary>
    /// Rule 4, the real pipeline: tokens are signed and validated with the key the secret store
    /// serves (a local <c>Secrets:</c> override in this harness), never a values key.
    /// </summary>
    [Fact]
    public async Task TokenSigningKey_ComesFromTheSecretStore_ThroughTheRealPipeline()
    {
        TestConfiguration.Ensure();
        Assert.Null(ConfigurationLoader.ReadAmbient("EastFive.Security.Token.Key"));

        var rows = await ConfigurationLoader.InspectAsync<EastFive.Security.Tokens.TokenSigningConfiguration>(
            ConfigurationLoader.ReadAmbient, SecretStore.Current);
        var signingKey = rows.Single(row => row.Member.IsSecret);
        Assert.Equal(TestConfiguration.SigningKeyName, signingKey.Name);
        Assert.Equal($"{SecretSource.LocalOverride} ({ConfigurationSecretOverrides.Section})", signingKey.Source);

        var token = EastFive.Api.Auth.JwtTools.CreateToken((Guid?)Guid.NewGuid(), new Uri("https://tests.eastfive.invalid/api"),
            TimeSpan.FromMinutes(5), new Dictionary<string, string>(), (jwt, _) => jwt, missing => throw new InvalidOperationException(missing),
            (_, why) => throw new InvalidOperationException(why));
        var accepted = $"Bearer {token}".GetClaimsJwtString(claims => claims.Any(), why => false);
        Assert.True(accepted);

        // A token signed with any other key is refused by the same validation path.
        using var foreignRsa = System.Security.Cryptography.RSA.Create(2048);
        var foreign = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(foreignRsa.ToXmlString(true)));
        var forged = EastFive.Api.Auth.JwtTools.CreateToken((Guid?)Guid.NewGuid(), new Uri("https://tests.eastfive.invalid/api"),
            TimeSpan.FromMinutes(5), new Dictionary<string, string>(), (jwt, _) => jwt, missing => throw new InvalidOperationException(missing),
            (_, why) => throw new InvalidOperationException(why),
            signing: new EastFive.Security.Tokens.TokenSigning("https://tests.eastfive.invalid/", new Secret(foreign),
                EastFive.Security.Tokens.TokenSigning.DefaultAlgorithm));
        Assert.False($"Bearer {forged}".GetClaimsJwtString(claims => true, why => false));
    }
}
