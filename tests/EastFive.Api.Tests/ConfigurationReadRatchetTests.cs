using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using Xunit;

namespace EastFive.Api.Tests;

/// <summary>
/// Every configuration read inside EastFive goes through a typed <c>IProvideConfiguration</c>
/// loaded by <c>ConfigurationLoader</c>. This ratchet counts the RAW reads that remain per project:
/// the <c>ConfigurationString</c> family, <c>Settings.Get*</c>, <c>ConfigurationLoader.ReadAmbient</c>,
/// <c>ConfigurationManager.AppSettings</c>, direct <c>IConfiguration</c> indexing and
/// <c>Environment.GetEnvironmentVariable</c>. <c>src/EastFive.Core/Configuration/</c> — the
/// configuration bootstrap itself — is not scanned. A pin is EXACT: a new raw read fails it, and
/// removing one fails it until the pin is lowered, so the number only ever falls.
/// </summary>
public class ConfigurationReadRatchetTests
{
    [Fact]
    public void Core_RawConfigurationReads_OnlyFall() => ConfigurationReadRatchet.AssertPinned("Core", 0);

    [Fact]
    public void Api_RawConfigurationReads_OnlyFall() => ConfigurationReadRatchet.AssertPinned("Api", 0);

    [Fact]
    public void TheScannerCountsEveryRawForm()
    {
        foreach (var line in new[]
        {
            "var x = AppSettings.Key.ConfigurationString(v => v);",
            "    .ConfigurationLong(",
            "Settings.GetGuid(AppSettings.Id, g => g, why => default);",
            "ConfigurationLoader.ReadAmbient(key)",
            "System.Configuration.ConfigurationManager.AppSettings[key]",
            "var v = configuration[\"Some.Key\"];",
            "Environment.GetEnvironmentVariable(\"WEBSITE_SITE_NAME\")",
        })
            Assert.True(ConfigurationReadRatchet.IsRawRead(line), line);
        Assert.False(ConfigurationReadRatchet.IsRawRead("// \"key\".ConfigurationString(x => x)"));
        Assert.False(ConfigurationReadRatchet.IsRawRead("StartupConfiguration.Get<TokenSigningConfiguration, string>(c => c.Issuer, _ => null)"));
    }
}

/// <summary>The scanner behind the configuration-read ratchet, shared with EastFive.Azure.Tests.</summary>
public static class ConfigurationReadRatchet
{
    /// <summary>Raw reads that ARE the configuration bootstrap, each with why it cannot be typed.
    /// Keyed by "{project-relative path}:{trimmed line}" so a moved line is a new decision.</summary>
    public static readonly IReadOnlyDictionary<string, string> Bootstrap = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Azure/Secrets/KeyVaultSecretStore.cs:_ => ManagedIdentity || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(\"WEBSITE_SITE_NAME\"))"] =
            "the secret store chooses its vault credential before configuration is initialized; App Service's own variable is the only signal there",
    };

    private static readonly Regex RawRead = new(
        @"\.\s*Configuration(String|Guid|Boolean|Long|Int|Uri|Base64Bytes|Certificate|Double|DateTime|Enum|Strings|Json)\s*[<(]"
        + @"|\bSettings\.Get(String|Uri|Guid|Base64Bytes|DateTime|Long|Double|Boolean|Bool|Int)\s*\("
        + @"|ConfigurationLoader\.ReadAmbient\b"
        + @"|ConfigurationManager\.AppSettings\b"
        + @"|\b(configuration|Configuration|config|_configuration)\s*\[\s*[^\]\s]"
        + @"|Environment\.GetEnvironmentVariable\s*\(",
        RegexOptions.Compiled);

    public static bool IsRawRead(string line)
    {
        var trimmed = line.TrimStart();
        return !trimmed.StartsWith("//") && !trimmed.StartsWith("*") && RawRead.IsMatch(line);
    }

    public static (string where, string line)[] RawReads(string project)
    {
        var root = Path.Combine(SecretReadGuard.SourceRoot(), $"EastFive.{project}");
        var excluded = Path.Combine(SecretReadGuard.SourceRoot(), "EastFive.Core", "Configuration") + Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && !path.StartsWith(excluded, StringComparison.Ordinal))
            .SelectMany(path =>
            {
                var relative = Path.GetRelativePath(SecretReadGuard.SourceRoot(), path).Replace(Path.DirectorySeparatorChar, '/');
                relative = relative.StartsWith("EastFive.") ? relative["EastFive.".Length..] : relative;
                return File.ReadAllLines(path)
                    .Select((text, index) => (where: $"{relative}:{index + 1}", key: $"{relative}:{text.Trim()}", text))
                    .Where(entry => IsRawRead(entry.text) && !Bootstrap.ContainsKey(entry.key))
                    .Select(entry => (entry.where, entry.text.Trim()));
            })
            .ToArray();
    }

    public static void AssertPinned(string project, int pinned)
    {
        var reads = RawReads(project);
        Assert.True(reads.Length == pinned,
            reads.Length > pinned
                ? $"EastFive.{project} has {reads.Length} raw configuration reads (pinned {pinned}). Read through a typed IProvideConfiguration instead:{Environment.NewLine}"
                    + string.Join(Environment.NewLine, reads.Select(read => $"  {read.where}  {read.line}"))
                : $"EastFive.{project} is down to {reads.Length} raw configuration reads: lower the pin from {pinned} to {reads.Length}.");
    }
}
