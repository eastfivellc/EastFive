using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

using Xunit;

using EastFive.Configuration;

namespace EastFive.Api.Tests;

/// <summary>
/// The guard on the model "framework code reads a secret only through a typed configuration":
/// a source scan of every framework project for <c>ConfigurationString</c>-family reads, each
/// key resolved to its value and refused if it is a retired secret key or secret-shaped; plus
/// <c>new Secret(...)</c> only inside the loader and the stores, and no framework code installing
/// startup configuration it did not load. Unresolvable (dynamic) keys must be listed here with
/// the reason they cannot be a secret.
/// </summary>
public class SecretReadGuardTests
{
    /// <summary>This project scans Core and Api with their constants; EastFive.Azure.Tests scans
    /// Azure and Sheets with the same guard (it references this project).</summary>
    private static readonly string[] ScannedProjects = { "Core", "Api" };

    [Fact]
    public void NoCoreOrApiCode_ReadsASecretFromTheValuesStore()
        => SecretReadGuard.AssertNoSecretReads(ScannedProjects, new[] { typeof(Secret).Assembly, typeof(ApiKeyConfiguration).Assembly });

    [Fact]
    public void RetiredSecretKeys_AppearNowhereInCoreOrApi()
        => SecretReadGuard.AssertRetiredKeysAbsent(ScannedProjects);

    [Fact]
    public void CoreAndApi_MakeSecretsOnlyInTheLoaderAndStores()
        => SecretReadGuard.AssertSecretsMadeOnlyByLoader(ScannedProjects);

    [Fact]
    public void TheGuardItselfCatchesASecretRead()
    {
        // A regression test for the scanner: these lines must be flagged.
        Assert.Matches(SecretReadGuard.ReadCall, "return EastFive.Security.AppSettings.TokenKey.ConfigurationString(x => x);");
        Assert.Matches(SecretReadGuard.ReadCall, "Settings.GetString(\"EastFive.Api.ApiSecurityKey\", k => k, why => null)");
        Assert.Matches(SecretReadGuard.SecretShape, "EastFive.Google.ClientSecret");
        Assert.DoesNotMatch(SecretReadGuard.SecretShape, "EastFive.Google.ClientID");
    }
}

/// <summary>The scanner behind the guard tests, shared with EastFive.Azure.Tests.</summary>
public static class SecretReadGuard
{
    /// <summary>Every framework secret that used to be read from the values store by key. None
    /// may come back as a key, a constant, or a literal.</summary>
    public static readonly string[] RetiredSecretKeys =
    {
        "EastFive.Azure.StorageTables.ConnectionString",
        "EastFive.Azure.DataLake.ConnectionString",
        "EastFive.Azure.Spa.ConnectionString",
        "EastFive.Azure.StorageTables.Backup.Default",
        "EastFive.Azure.StorageTables.TableInformationToken",
        "EastFive.Azure.StorageTables.EncryptionString",
        "EastFive.Azure.StorageTables.EncryptionSalt",
        "EastFive.Api.Workers.ServiceBusConnectionString",
        "EastFive.Azure.ApplicationInsights.ClientSecret",
        "EastFive.Azure.ApplicationInsights.TeamsHook",
        "EastFive.Azure.Search.SearchServiceAdminApiKey",
        "EastFive.Azure.Auth.AdminLoginRsaKey",
        "EastFive.Azure.CognitiveServices.ComputerVisionSubscriptionKey",
        "EastFive.Security.CredentialProvider.SAML.Certificate",
        "EastFive.Azure.AzureADB2C.ClientSecret",
        "EastFive.Apple.ClientSecret",
        "EastFive.Google.ClientSecret",
        "EastFive.Microsoft.ClientSecret",
        "EastFive.Security.LoginProvider.PingIdentity.Athena.RestApiKey",
        "EastFive.Salesforce.ConsumerSecret",
        "EastFive.Azure.Communications.ConnectionString",
        "EastFive.Azure.AIFoundry.ClientSecret",
        "EastFive.Azure.Login.ClientSecret",
        "EastFive.Api.Security.SiteAdminAuthorization",
        "EastFive.Api.ApiSecurityKey",
        "EastFive.Api.AccessTokenSecret",
        "EastFive.Api.Postman.ApiKey",
        "EastFive.Security.Token.Key",
        "EastFive.Security.CredentialProvider.Voucher.Key",
        "Azure.Storage.Account.",
    };

    /// <summary>Keys whose shape looks secret but whose value is not (with the reason).</summary>
    private static readonly Dictionary<string, string> NotSecrets = new(StringComparer.Ordinal)
    {
        ["EastFive.Azure.ApplicationInsights.InstrumentationKey"] = "the instrumentation key ships to every browser in the SPA",
        ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = "ingestion-only; Microsoft documents it as not a secret",
    };

    /// <summary>Reads whose key is computed at run time, and why none can name a framework secret.</summary>
    private static readonly Dictionary<string, string> DynamicReads = new(StringComparer.Ordinal)
    {
        ["Core/Configuration/Configuration.cs"] = "the obsolete generic typed-key helper itself",
        ["Core/Configuration/ConfigurationExtensions.cs"] = "the obsolete ConfigurationString family itself",
    };

    public static readonly Regex ReadCall = new(
        @"(?<receiver>[A-Za-z_][\w.]*|""[^""]*"")\s*\.\s*Configuration(String|Guid|Uri|Base64Bytes|Long|Boolean|Double|DateTime|Json)\s*[<(]"
        + @"|Settings\.Get(String|Uri|Guid|Base64Bytes|DateTime|Long|Double|Boolean)\s*\(\s*(?<argument>[A-Za-z_][\w.]*|""[^""]*"")",
        RegexOptions.Compiled);

    public static readonly Regex SecretShape = new(
        @"(Secret|Password|ApiKey|ConnectionString|Token\.Key|\.Key|Certificate|Salt|Hook)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string SourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "EastFive.sln")))
            directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("EastFive.sln not found above the test output."), "src");
    }

    private static IEnumerable<(string relative, int line, string text)> FrameworkLines(string[] projects)
        => Directory.EnumerateFiles(SourceRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(path => projects.Any(project => Path.GetRelativePath(SourceRoot(), path).StartsWith($"EastFive.{project}{Path.DirectorySeparatorChar}")))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .SelectMany(path =>
            {
                var relative = Path.GetRelativePath(SourceRoot(), path).Replace(Path.DirectorySeparatorChar, '/');
                relative = relative.StartsWith("EastFive.") ? relative["EastFive.".Length..] : relative;
                return File.ReadAllLines(path)
                    .Select((text, index) => (relative, line: index + 1, text))
                    .Where(entry => !entry.text.TrimStart().StartsWith("//"));
            });

    /// <summary>Every public const string in the framework assemblies, by every dotted suffix of its path.</summary>
    private static ILookup<string, string> ConstantsBySuffix(IEnumerable<Assembly> assemblies)
        => assemblies
            .Distinct()
            .SelectMany(assembly => assembly.GetTypes())
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral && field.FieldType == typeof(string))
                .Select(field => (path: $"{type.FullName!.Replace('+', '.')}.{field.Name}", value: (string)field.GetRawConstantValue()!)))
            .SelectMany(constant =>
            {
                var segments = constant.path.Split('.');
                return Enumerable.Range(0, segments.Length).Select(skip => (suffix: string.Join('.', segments.Skip(skip)), constant.value));
            })
            .ToLookup(pair => pair.suffix, pair => pair.value);

    public static void AssertNoSecretReads(string[] projects, IEnumerable<Assembly> constantAssemblies)
    {
        var constants = ConstantsBySuffix(constantAssemblies);
        var offences = FrameworkLines(projects)
            .SelectMany(entry => ReadCall.Matches(entry.text).Select(match => (entry, match)))
            .Select(pair =>
            {
                var expression = pair.match.Groups["receiver"].Success ? pair.match.Groups["receiver"].Value : pair.match.Groups["argument"].Value;
                var resolved = expression.StartsWith('"')
                    ? new[] { expression.Trim('"') }
                    : constants[expression].Distinct().ToArray();
                return (pair.entry, expression, resolved);
            })
            .Select(read => read.resolved.Length switch
            {
                0 => DynamicReads.ContainsKey(read.entry.relative)
                    ? null
                    : $"{read.entry.relative}:{read.entry.line} reads [{read.expression}], which resolves to no framework constant; list it in DynamicReads with why it cannot be a secret",
                _ => read.resolved
                    .Where(key => RetiredSecretKeys.Any(retired => key.StartsWith(retired, StringComparison.Ordinal))
                        || (SecretShape.IsMatch(key) && !NotSecrets.ContainsKey(key)))
                    .Select(key => $"{read.entry.relative}:{read.entry.line} reads secret-shaped key [{key}] from the values store")
                    .FirstOrDefault(),
            })
            .OfType<string>()
            .ToArray();
        Assert.True(offences.Length == 0, string.Join(Environment.NewLine, offences));
    }

    public static void AssertRetiredKeysAbsent(string[] projects)
    {
        var offences = FrameworkLines(projects)
            .SelectMany(entry => RetiredSecretKeys
                .Where(key => entry.text.Contains($"\"{key}", StringComparison.Ordinal))
                .Select(key => $"{entry.relative}:{entry.line} mentions retired secret key [{key}]"))
            .ToArray();
        Assert.True(offences.Length == 0, string.Join(Environment.NewLine, offences));
    }

    private static readonly string[] SecretFactories =
    {
        "Core/Configuration/ConfigurationLoader.cs",
        "Core/Configuration/SecretStore.cs",
        "Core/Configuration/ConfigurationValueConverter.cs",
        "Azure/Secrets/KeyVaultSecretStore.cs",
    };

    public static void AssertSecretsMadeOnlyByLoader(string[] projects)
    {
        var lines = FrameworkLines(projects).ToArray();
        var minted = lines
            .Where(entry => Regex.IsMatch(entry.text, @"new\s+(EastFive\.Configuration\.)?Secret\s*\(") && !SecretFactories.Contains(entry.relative))
            .Select(entry => $"{entry.relative}:{entry.line} constructs a Secret outside the loader/stores")
            .ToArray();
        var installed = lines
            .Where(entry => Regex.IsMatch(entry.text, @"StartupConfiguration\.Set\s*[<(]") && entry.relative != "Core/Configuration/StartupConfiguration.cs")
            .Select(entry => $"{entry.relative}:{entry.line} installs startup configuration it did not load")
            .ToArray();
        var offences = minted.Concat(installed).ToArray();
        Assert.True(offences.Length == 0, string.Join(Environment.NewLine, offences));
    }

}
