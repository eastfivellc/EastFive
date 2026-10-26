using System;
using System.Collections.Generic;
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
        Assert.Null(TestConfiguration.Values!["EastFive.Security.Token.Key"]);

        var rows = await ConfigurationLoader.InspectAsync<EastFive.Security.Tokens.TokenSigningConfiguration>();
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

/// <summary>Google client ids carry dots (<c>{project}-{id}.apps.googleusercontent.com</c>), which
/// cannot be a secret-name segment; the Google configuration drops the constant domain.</summary>
public class GoogleSecretNameTests
{
    [Fact]
    public async Task GoogleClientId_NamesItsSecretWithoutTheGoogleDomain()
    {
        const string clientId = "445833953703-a5ighep676fu7ndo3407eqg8pegvm5mj.apps.googleusercontent.com";
        var google = new EastFive.Azure.Auth.GoogleLoginConfiguration { ClientId = clientId };
        Assert.Equal("Google-445833953703-a5ighep676fu7ndo3407eqg8pegvm5mj-ClientSecret",
            ((IProvideConfiguration)google).SecretReference("ClientSecret").Name);

        var secrets = new InMemorySecretStore(new Dictionary<string, string>
        {
            ["Google-445833953703-a5ighep676fu7ndo3407eqg8pegvm5mj-ClientSecret"] = "k-google",
        });
        var outcome = await ConfigurationLoader.LoadAsync<EastFive.Azure.Auth.GoogleLoginConfiguration, string>(
            key => key == EastFive.Azure.Auth.GoogleLoginConfiguration.ClientIdKey ? clientId : null, secrets,
            config => $"{config.ClientId}|{config.ClientSecret.Reveal()}",
            issue => $"missing:{issue.Key}", issue => $"invalid:{issue.Key}", issue => $"failed:{issue.Key}");
        Assert.Equal($"{clientId}|k-google", outcome);
    }
}

/// <summary>
/// The audit behind the account rule: every framework account member either translates its value
/// (a declared <see cref="IProvideSecretAccountSegment"/>) or names a vendor id whose format is
/// always vault-safe — and that claim is checked against a real-shaped id for each one.
/// </summary>
public class AccountMemberAuditTests
{
    /// <summary>Configuration type → a real-shaped account value, and why the format is always safe.</summary>
    private static readonly Dictionary<string, (string sample, string why)> SafeByFormat = new()
    {
        [nameof(EastFive.Azure.Persistence.StorageTablesConfiguration)] = ("rosemaryprod", "Azure storage account names are 3-24 lowercase letters and digits"),
        [nameof(EastFive.Azure.Persistence.SpaStorageConfiguration)] = ("rosemaryprod", "Azure storage account names are 3-24 lowercase letters and digits"),
        [nameof(EastFive.Azure.Persistence.DataLakeStorageConfiguration)] = ("affirmdatalake", "Azure storage account names are 3-24 lowercase letters and digits"),
        [nameof(EastFive.Azure.Auth.MicrosoftLoginConfiguration)] = ("c4f2a1d3-0b9e-4f6a-8d7c-2e1b3a4c5d6e", "Entra application ids are GUIDs"),
        [nameof(EastFive.Azure.ApplicationInsightsQueryConfiguration)] = ("7ef17a55-0e6e-42b7-a5d5-ba6db737a54a", "Application Insights application ids are GUIDs"),
        [nameof(EastFive.Azure.ResourceManagement.AzureClientApplicationConfiguration)] = ("c4f2a1d3-0b9e-4f6a-8d7c-2e1b3a4c5d6e", "Entra application ids are GUIDs"),
    };

    [Fact]
    public void EveryFrameworkAccountMember_TranslatesOrIsSafeByFormat()
    {
        var catalog = ConfigurationCatalog.For(new[] { typeof(EastFive.Azure.Persistence.StorageAccount).Assembly, typeof(Secret).Assembly, typeof(EastFive.Api.ApiKeyConfiguration).Assembly });
        var withAccounts = catalog.Where(type => type.Members.Any(member => member.IsAccount)).ToArray();
        Assert.NotEmpty(withAccounts);
        Assert.All(withAccounts, type =>
        {
            var account = type.Members.Single(member => member.IsAccount);
            if (account.AccountSegmentTranslation != null)
                return;
            Assert.True(SafeByFormat.TryGetValue(type.Type.Name, out var audit),
                $"{type.Type.Name}.{account.Property} names secrets with its raw value: declare a translation or record why its format is always vault-safe.");
            Assert.Null(type.AccountProblem(audit.sample));
        });
        Assert.Equal("GoogleClientIdSegmentAttribute",
            catalog.Single(type => type.Type == typeof(EastFive.Azure.Auth.GoogleLoginConfiguration)).Members.Single(member => member.IsAccount).AccountSegmentTranslation);
    }
}
