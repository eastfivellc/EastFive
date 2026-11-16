using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Xunit;

using EastFive.Api.Tests.Harness;
using EastFive.Azure.Auth;
using EastFive.Azure.Tests.OAuth;
using EastFive.Configuration;

namespace EastFive.Azure.Tests;

/// <summary>
/// The SuperAdmin <c>Configuration</c> endpoint through the real dispatch pipeline: it renders every
/// framework configuration type from the catalog, lists a keyed family's configured instances with
/// their values, never carries a secret's value, and refuses a caller without the role.
/// </summary>
[Collection(AmbientConfigurationCollection.Name)]
public class ConfigurationEndpointTests : TestSession
{
    private static readonly string Tag = "endpoint" + Guid.NewGuid().ToString("N")[..8];

    /// <summary>A keyed type whose instance key names its secrets, any id allowed.</summary>
    [ConfigurationType("EndpointHook", Label = "Endpoint hook", Description = "Fixture: a keyed type with a secret.", Scope = ConfigurationScope.Environment)]
    private sealed class EndpointHook : IProvideConfiguration, IProvideConfigurationInstanceKey
    {
        public EndpointHook(string id) => InstanceKey = id;

        [EncodedSecretAccountSegment]
        public string InstanceKey { get; }

        public const string UrlKeyFormat = "EastFive.Tests.EndpointHook.{0}.Url";

        [ConfigurationMember("EastFive.Tests.EndpointHook.{key}.Url", Kind = ConfigurationMemberKind.Optional, Label = "URL", Description = "Where it posts.")]
        public string? Url { get; set; }

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "Signing secret", Description = "Signs each post.")]
        public Secret SigningSecret { get; set; } = default!;
    }

    static ConfigurationEndpointTests()
    {
        TestConfiguration.Ensure();
        Environment.SetEnvironmentVariable(string.Format(PingRedirectConfiguration.AuthNameKeyFormat, Tag), "endpoint-auth");
        Environment.SetEnvironmentVariable(string.Format(EndpointHook.UrlKeyFormat, Tag), "https://hooks.example/plain");
        Environment.SetEnvironmentVariable(string.Format(EndpointHook.UrlKeyFormat, Tag + "_x"), "https://hooks.example/encoded");
        EastFive.Web.Configuration.ConfigurationExtensions.Initialize(new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build());
    }

    private Task<ResponseBranchCapture> GetAsync(string? bearer)
        => DispatchRawAsync(typeof(EastFive.Azure.Configuration.Configuration).GetMethod(nameof(EastFive.Azure.Configuration.Configuration.GetAsync))!,
            OAuthRequests.Bare(HttpMethod.Get, "/api/Configuration", bearer));

    [Fact]
    public async Task SuperAdmin_SeesEveryFrameworkType_KeyedInstances_AndNoSecretValues()
    {
        var capture = await GetAsync(OAuthFixtures.SuperAdminToken());

        Assert.True(capture.TryGet("onFound", out var args),
            $"branch={capture.BranchName} status={capture.Response?.StatusCode} reason={capture.Response?.ReasonPhrase}");
        var rows = Assert.IsType<EastFive.Azure.Configuration.Configuration[]>(args[0]);

        var framework = ConfigurationCatalog.For(new[]
        {
            typeof(Secret).Assembly,
            typeof(EastFive.Api.ApiKeyConfiguration).Assembly,
            typeof(EastFive.Azure.Persistence.StorageAccount).Assembly,
            typeof(EastFive.Sheets.ISheet).Assembly,
        });
        var missing = framework
            .SelectMany(type => type.Members.Select(member => (type: type.TypeName ?? type.Type.Name, member.Property)))
            .Where(expected => !rows.Any(row => row.configurationType == expected.type && row.name == expected.Property))
            .ToArray();
        Assert.Empty(missing);

        Assert.All(rows.Where(row => row.kind == nameof(ConfigurationMemberKind.Secret)), row => Assert.Null(row.value));

        var instance = Assert.Single(rows, row => row.instance == Tag && row.name == nameof(PingRedirectTagConfiguration.PingAuthName));
        Assert.Equal(string.Format(PingRedirectConfiguration.AuthNameKeyFormat, Tag), instance.key);
        Assert.Equal("endpoint-auth", instance.value);
    }

    [Fact]
    public async Task AKeyedInstance_ShowsTheSecretNameItReads()
    {
        var capture = await GetAsync(OAuthFixtures.SuperAdminToken());

        Assert.True(capture.TryGet("onFound", out var args),
            $"branch={capture.BranchName} status={capture.Response?.StatusCode} reason={capture.Response?.ReasonPhrase}");
        var secrets = Assert.IsType<EastFive.Azure.Configuration.Configuration[]>(args[0])
            .Where(row => row.configurationType == "EndpointHook" && row.name == nameof(EndpointHook.SigningSecret))
            .ToArray();

        // The type's own row keeps the template; each instance's row names its real secret.
        Assert.Equal("EndpointHook-{account}-SigningSecret", Assert.Single(secrets, row => row.instance == null).secretName);
        Assert.Equal($"EndpointHook-{Tag}-SigningSecret", Assert.Single(secrets, row => row.instance == Tag).secretName);
        Assert.Equal($"EndpointHook-e{Tag}--5fx-SigningSecret", Assert.Single(secrets, row => row.instance == Tag + "_x").secretName);
        Assert.All(secrets, row => Assert.Null(row.value));
    }

    [Fact]
    public async Task WithoutTheRole_IsRefused()
    {
        var capture = await GetAsync(bearer: null);

        Assert.False(capture.TryGet("onFound", out _));
        Assert.True(capture.Response!.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"status={capture.Response.StatusCode}");
    }
}
