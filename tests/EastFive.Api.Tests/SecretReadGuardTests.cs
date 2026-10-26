using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using Xunit;


namespace EastFive.Api.Tests;

/// <summary>
/// The guard on the model "framework code reads a secret only through a typed configuration".
/// Reading one by key is structurally impossible (there is no public raw read — see
/// <see cref="ConfigurationReadGuard"/>), so what remains is: no retired secret key comes back,
/// <c>new Secret(...)</c> only inside the loader and the stores, and no framework code installing
/// startup configuration it did not load.
/// </summary>
public class SecretReadGuardTests
{
    /// <summary>This project scans Core and Api with their constants; EastFive.Azure.Tests scans
    /// Azure and Sheets with the same guard (it references this project).</summary>
    private static readonly string[] ScannedProjects = { "Core", "Api" };

    [Fact]
    public void RetiredSecretKeys_AppearNowhereInCoreOrApi()
        => SecretReadGuard.AssertRetiredKeysAbsent(ScannedProjects);

    [Fact]
    public void CoreAndApi_MakeSecretsOnlyInTheLoaderAndStores()
        => SecretReadGuard.AssertSecretsMadeOnlyByLoader(ScannedProjects);
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
