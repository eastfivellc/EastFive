using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Xunit;

using EastFive.Api.Binding;
using EastFive.Api.Bindings;
using EastFive.Api.Tests.Harness;

namespace EastFive.Api.Tests;

/// <summary>Tests that swap the ambient configuration run alone.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AmbientConfigurationCollection
{
    public const string Name = "Ambient configuration";
}

[CorsHandler]
[CaptureResponseBranches]
[StandardStringBindings]
[StandardJTokenBindings]
public sealed class CorsTestApplication : HttpApplication
{
    public CorsTestApplication(IConfiguration configuration) : base(configuration) { }
}

[FunctionViewController(Route = "cors-probe")]
public static class CorsProbe
{
    [HttpOptions]
    public static IHttpResponse Preflight(NoContentResponse onReached) => onReached();
}

/// <summary>
/// CORS through the real pipeline, read from the typed <see cref="CorsConfiguration"/> at use: the
/// application-level <see cref="CorsHandlerAttribute"/> answers a preflight for a configured origin
/// with the configured max age, refuses an unknown origin, and stays out of the way when disabled.
/// </summary>
[Collection(AmbientConfigurationCollection.Name)]
public class CorsConfigurationPipelineTests : TestSession
{
    private static async Task<EastFive.Api.Tests.Harness.ResponseBranchCapture> PreflightAsync(IReadOnlyDictionary<string, string?> values, string origin)
    {
        var original = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true).AddEnvironmentVariables().Build();
        var configuration = new ConfigurationBuilder().AddConfiguration(original).AddInMemoryCollection(values).Build();
        EastFive.Web.Configuration.ConfigurationExtensions.Initialize(configuration);
        try
        {
            var request = new EastFive.Api.HttpRequest(new Uri("https://api.server.example/api/cors-probe"))
            {
                Method = HttpMethod.Options,
            };
            request.Headers["Origin"] = new[] { origin };
            request.Headers["Access-Control-Request-Method"] = new[] { "GET" };
            var capture = await new TestSession().DispatchRawAsync(new CorsTestApplication(configuration),
                typeof(CorsProbe).GetMethod(nameof(CorsProbe.Preflight))!, request);
            return capture;
        }
        finally
        {
            EastFive.Web.Configuration.ConfigurationExtensions.Initialize(original);
        }
    }

    private static string? Header(IHttpResponse response, string name)
        => response.Headers.TryGetValue(name, out var values) ? values.FirstOrDefault() : null;

    [Fact]
    public async Task ConfiguredOrigin_GetsThePreflightAnswer_WithTheConfiguredMaxAge()
    {
        var response = await PreflightAsync(new Dictionary<string, string?>
        {
            [EastFive.Api.CorsConfiguration.EnabledKey] = "true",
            ["cors:Origins"] = "https://app.partner.example, https://other.example",
            ["cors:MaxAgeSeconds"] = "600",
        }, "https://app.partner.example");
        Assert.Null(response.BranchName); // answered by the handler, not the method
        Assert.Equal(HttpStatusCode.OK, response.Response!.StatusCode);
        Assert.Equal("https://app.partner.example", Header(response.Response, "Access-Control-Allow-Origin"));
        Assert.Equal("600", Header(response.Response, "Access-Control-Max-Age"));
    }

    [Fact]
    public async Task UnknownOrigin_IsRefused()
    {
        var response = await PreflightAsync(new Dictionary<string, string?>
        {
            [EastFive.Api.CorsConfiguration.EnabledKey] = "true",
            ["cors:Origins"] = "https://app.partner.example",
        }, "https://evil.example");
        Assert.Null(response.BranchName);
        Assert.Equal(HttpStatusCode.Forbidden, response.Response!.StatusCode);
    }

    [Fact]
    public async Task Disabled_ReachesTheMethod_WithNoCorsHeaders()
    {
        var response = await PreflightAsync(new Dictionary<string, string?>
        {
            ["cors:Origins"] = "https://app.partner.example",
        }, "https://app.partner.example");
        Assert.Equal("onReached", response.BranchName);
        Assert.Null(Header(response.Response!, "Access-Control-Allow-Origin"));
    }
}
