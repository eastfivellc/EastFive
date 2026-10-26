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
/// Every configuration read inside EastFive goes through a typed <c>IProvideConfiguration</c>
/// loaded by <c>ConfigurationLoader</c>. The raw read API is gone, so the guard is structural:
/// no framework assembly exposes a public method that reads a key, and no framework source outside
/// <c>src/EastFive.Core/Configuration/</c> calls the loader's internal reads, indexes an
/// <c>IConfiguration</c>, or reads <c>ConfigurationManager</c> or the environment.
/// </summary>
public class ConfigurationReadGuardTests
{
    [Fact]
    public void NoFrameworkAssembly_ExposesARawConfigurationRead()
        => ConfigurationReadGuard.AssertNoPublicRawRead(typeof(Secret).Assembly, typeof(ApiKeyConfiguration).Assembly);

    [Fact]
    public void Core_HasNoRawConfigurationReads() => ConfigurationReadGuard.AssertNoRawReads("Core");

    [Fact]
    public void Api_HasNoRawConfigurationReads() => ConfigurationReadGuard.AssertNoRawReads("Api");

    [Fact]
    public void TheInstallerIsTheOnlyPublicAmbientMember()
        => Assert.Equal(new[] { nameof(EastFive.Web.Configuration.ConfigurationExtensions.Initialize) },
            typeof(EastFive.Web.Configuration.ConfigurationExtensions)
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(method => method.Name));

    [Fact]
    public void TheScannerCountsEveryRawForm()
    {
        foreach (var line in new[]
        {
            "ConfigurationLoader.ReadAmbient(key)",
            "var v = EastFive.Web.Configuration.ConfigurationExtensions.ReadValue(key);",
            "ConfigurationExtensions.SectionJson(key)",
            "System.Configuration.ConfigurationManager.AppSettings[key]",
            "var v = configuration[\"Some.Key\"];",
            "Environment.GetEnvironmentVariable(\"WEBSITE_SITE_NAME\")",
        })
            Assert.True(ConfigurationReadGuard.IsRawRead(line), line);
        Assert.False(ConfigurationReadGuard.IsRawRead("// ConfigurationLoader.ReadAmbient(key)"));
        Assert.False(ConfigurationReadGuard.IsRawRead("StartupConfiguration.Get<TokenSigningConfiguration, string>(c => c.Issuer, _ => null)"));
    }
}

/// <summary>The structural configuration-read guard, shared with EastFive.Azure.Tests.</summary>
public static class ConfigurationReadGuard
{
    /// <summary>Raw reads that ARE the configuration bootstrap, each with why it cannot be typed.
    /// Keyed by "{project-relative path}:{trimmed line}" so a moved line is a new decision.</summary>
    public static readonly IReadOnlyDictionary<string, string> Bootstrap = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Azure/Secrets/KeyVaultSecretStore.cs:_ => ManagedIdentity || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(\"WEBSITE_SITE_NAME\"))"] =
            "the secret store chooses its vault credential before configuration is initialized; App Service's own variable is the only signal there",
    };

    /// <summary>The raw read API EastFive used to expose, by member name. None may come back public.</summary>
    private static readonly Regex RetiredReadName = new(
        @"^(Configuration(String|Guid|Boolean|Long|Int|Uri|Base64Bytes|Certificate|Double|DateTime|Enum|Strings|Json)|Get(String|Uri|Guid|Base64Bytes|DateTime|Double)|ReadAmbient|ReadValue|SectionJson|ConfiguredKeys)$",
        RegexOptions.Compiled);

    /// <summary>In-assembly reads of the loader's internal values reader, and the reads EastFive never
    /// owned (<c>ConfigurationManager</c>, <c>IConfiguration</c> indexing, the environment).</summary>
    private static readonly Regex RawRead = new(
        @"ConfigurationLoader\.ReadAmbient\b"
        + @"|ConfigurationExtensions\.(ReadValue|SectionJson|ConfiguredKeys)\b"
        + @"|ConfigurationManager\.AppSettings\b"
        + @"|\b(configuration|Configuration|config|_configuration)\s*\[\s*[^\]\s]"
        + @"|Environment\.GetEnvironmentVariable\s*\(",
        RegexOptions.Compiled);

    public static void AssertNoPublicRawRead(params Assembly[] assemblies)
    {
        var offences = assemblies
            .Distinct()
            .SelectMany(assembly => assembly.GetExportedTypes())
            .Where(type => type.Namespace is "EastFive.Configuration" or "EastFive.Web.Configuration" or "EastFive.Web")
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => RetiredReadName.IsMatch(method.Name))
                .Select(method => $"{type.FullName}.{method.Name}"))
            .Concat(assemblies
                .SelectMany(assembly => assembly.GetExportedTypes())
                .Where(type => type.FullName is "EastFive.Web.Configuration.Settings" or "EastFive.Configuration.IProvideConfigurationKey")
                .Select(type => type.FullName!))
            .ToArray();
        Assert.True(offences.Length == 0,
            $"Configuration is read through a typed IProvideConfiguration; these expose a raw read:{Environment.NewLine}"
                + string.Join(Environment.NewLine, offences));
    }

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

    public static void AssertNoRawReads(string project)
    {
        var reads = RawReads(project);
        Assert.True(reads.Length == 0,
            $"EastFive.{project} reads configuration raw. Read through a typed IProvideConfiguration instead:{Environment.NewLine}"
                + string.Join(Environment.NewLine, reads.Select(read => $"  {read.where}  {read.line}")));
    }
}
