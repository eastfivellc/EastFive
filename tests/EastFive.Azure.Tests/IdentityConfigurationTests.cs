using System;
using System.Linq;

using Microsoft.Extensions.Configuration;
using Xunit;

using EastFive.Azure.Auth;
using EastFive.Configuration;

namespace EastFive.Azure.Tests;

/// <summary>
/// The identity settings as consumers write them, read through the AMBIENT configuration the
/// framework sees in production: the keyed Ping and SAML families per tag (with the legacy Ping
/// spelling as fallback), the catalog enumerating the configured tags, and login redirections
/// written either as a JSON string or as a structured appsettings section.
/// </summary>
public class IdentityConfigurationTests
{
    // Unique per run so concurrently running tests never see these tags, and nothing they set is
    // visible under a key another test reads.
    private static readonly string NewTag = "pingnew" + Guid.NewGuid().ToString("N")[..8];
    private static readonly string LegacyTag = "pinglegacy" + Guid.NewGuid().ToString("N")[..8];
    private static readonly string SamlTag = "saml" + Guid.NewGuid().ToString("N")[..8];
    private static readonly Guid NewReportSet = Guid.NewGuid();
    private static readonly Guid LegacyReportSet = Guid.NewGuid();

    static IdentityConfigurationTests()
    {
        TestConfiguration.Ensure();
        Environment.SetEnvironmentVariable(string.Format(PingRedirectConfiguration.AuthNameKeyFormat, NewTag), "new-auth");
        Environment.SetEnvironmentVariable(string.Format(PingRedirectConfiguration.ReportSetIdKeyFormat, NewTag), NewReportSet.ToString());
        Environment.SetEnvironmentVariable($"AffirmHealth.PDMS.PingRedirect.{LegacyTag}.PingAuthName", "legacy-auth");
        Environment.SetEnvironmentVariable($"AffirmHealth.PDMS.PingRedirect.{LegacyTag}.PingReportSetId", LegacyReportSet.ToString());
        Environment.SetEnvironmentVariable(AppSettings.SAML.GetLaunchPage(SamlTag), "/launch/here");
        Environment.SetEnvironmentVariable(AppSettings.SAML.GetMetadataLocation(SamlTag), "https://idp.example/metadata");
        // The same sources TestConfiguration initializes from, so concurrent tests see what they saw.
        EastFive.Web.Configuration.ConfigurationExtensions.Initialize(new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build());
    }

    [Fact]
    public void Ping_ReadsEachTag_AndFallsBackToTheLegacySpelling()
    {
        Assert.Equal("new-auth", PingRedirectConfiguration.GetPingAuthName(NewTag, name => name, why => why));
        Assert.Equal(NewReportSet, PingRedirectConfiguration.GetPingReportSetId(NewTag, id => id, _ => Guid.Empty));
        Assert.Equal("legacy-auth", PingRedirectConfiguration.GetPingAuthName(LegacyTag, name => name, why => why));
        Assert.Equal(LegacyReportSet, PingRedirectConfiguration.GetPingReportSetId(LegacyTag, id => id, _ => Guid.Empty));
        Assert.Contains("is not specified", PingRedirectConfiguration.GetPingAuthName("unconfigured-tag", name => name, why => why));
    }

    [Fact]
    public void Saml_ReadsEachTag()
    {
        Assert.Equal("/launch/here", SamlTagConfiguration.WithLaunchPage(SamlTag, page => page, why => why));
        Assert.Equal(new Uri("https://idp.example/metadata"), SamlTagConfiguration.WithMetadataLocation(SamlTag, uri => uri, _ => null!));
        Assert.Null(SamlTagConfiguration.WithLaunchPage("unconfigured-tag", page => page, _ => null));
    }

    [Fact]
    public void Catalog_EnumeratesTheConfiguredTags()
    {
        var pingTags = ConfigurationCatalog.InstanceKeys(typeof(PingRedirectTagConfiguration));
        Assert.Contains(NewTag, pingTags);
        Assert.Contains(LegacyTag, pingTags);
        Assert.Contains(SamlTag, ConfigurationCatalog.InstanceKeys(typeof(SamlTagConfiguration)));
    }

    [Fact]
    public void Redirections_ReadAsAJsonString_OrAStructuredSection()
    {
        static string[] Keys(IConfiguration configuration)
        {
            var original = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false).AddEnvironmentVariables().Build();
            EastFive.Web.Configuration.ConfigurationExtensions.Initialize(
                new ConfigurationBuilder().AddConfiguration(original).AddConfiguration(configuration).Build());
            try
            {
                return SessionServerConfiguration.WithRedirections<RedirectionConfiguration[], string[]>(
                    rules => rules.Select(rule => $"{rule.Key}={rule.Value}").ToArray(),
                    why => new[] { $"invalid: {why}" },
                    () => Array.Empty<string>());
            }
            finally
            {
                EastFive.Web.Configuration.ConfigurationExtensions.Initialize(original);
            }
        }

        var asString = new ConfigurationBuilder().AddInMemoryCollection(new[]
        {
            new System.Collections.Generic.KeyValuePair<string, string?>(AppSettings.Auth.Redirections,
                "[{\"Key\":\"a\",\"Value\":\"https://a.example/\"}]"),
        }).Build();
        Assert.Equal(new[] { "a=https://a.example/" }, Keys(asString));

        var asSection = new ConfigurationBuilder().AddInMemoryCollection(new[]
        {
            new System.Collections.Generic.KeyValuePair<string, string?>($"{AppSettings.Auth.Redirections}:0:Key", "b"),
            new System.Collections.Generic.KeyValuePair<string, string?>($"{AppSettings.Auth.Redirections}:0:Value", "https://b.example/"),
            new System.Collections.Generic.KeyValuePair<string, string?>($"{AppSettings.Auth.Redirections}:1:Key", "c"),
            new System.Collections.Generic.KeyValuePair<string, string?>($"{AppSettings.Auth.Redirections}:1:Value", "https://c.example/"),
        }).Build();
        Assert.Equal(new[] { "b=https://b.example/", "c=https://c.example/" }, Keys(asSection));
    }
}
