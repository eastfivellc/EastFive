using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;

using EastFive.Api;
using EastFive.Api.Binding;
using EastFive.Api.Routing;

namespace EastFive.Api.Tests.Harness;

/// <summary>
/// Test-time application over the bare framework <see cref="HttpApplication"/>.
/// The <see cref="CaptureResponseBranchesAttribute"/> adds a single
/// <c>IHandleMethodInvocation</c> handler that swaps in stub response
/// delegates and records their invocations. Shared so attribute discovery
/// happens once per process.
/// </summary>
[CaptureResponseBranches]
public sealed class TestApplication : HttpApplication
{
    private static readonly Lazy<TestApplication> instance = new(BuildShared);

    public TestApplication(IConfiguration configuration) : base(configuration) { }

    public static TestApplication Shared => instance.Value;

    private static TestApplication BuildShared()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();
        EastFive.Web.Configuration.ConfigurationExtensions.Initialize(configuration);
        return new TestApplication(configuration);
    }
}

/// <summary>
/// Per-test base class (xUnit constructs one instance per <c>[Fact]</c>).
/// Dispatches controller methods through the production
/// envelope → match → dispatch pipeline, bypassing only URL routing.
/// </summary>
public class TestSession
{
    public TestApplication Application => TestApplication.Shared;

    /// <summary>
    /// Raw-request dispatch: runs the request through
    /// <see cref="MethodDispatcher.PickDeserializerAsync"/> so the REAL
    /// request envelope (e.g. the JSON envelope for application/json bodies)
    /// serves binding — the exact production path, mirroring Middleware's
    /// V3-first fork. Use this for regressions in envelope/binder behavior;
    /// typed-envelope dispatch would bypass the code under test.
    /// </summary>
    public async Task<ResponseBranchCapture> DispatchRawAsync(MethodInfo method, IHttpRequest request)
    {
        var candidate = CandidateFor(method);

        var capture = new ResponseBranchCapture();
        CaptureScope.Current = capture;
        try
        {
            var didPick = false;
            var app = this.Application;
            var handlers = ApplicationHandlers.For(app);
            var response = await MethodDispatcher
                .PickDeserializerAsync(handlers, request,
                    async (envelope) =>
                    {
                        didPick = true;
                        if (MethodDispatcherV3.ShouldDispatch(method))
                        {
                            var v3Matches = MethodDispatcherV3.BuildMatches(envelope, request,
                                new[] { candidate });
                            return await MethodDispatcherV3.DispatchAsync(app, handlers, request, v3Matches);
                        }

                        var matches = MethodDispatcher.BuildMatches(envelope, new[] { candidate });
                        return await MethodDispatcher.DispatchAsync(app, handlers, request, matches);
                    });
            if (!didPick)
                throw new InvalidOperationException(
                    $"No deserializer claimed the test request " +
                    $"({request.Method?.Method} {request.RequestUri?.AbsolutePath}): " +
                    $"status={response?.StatusCode}");

            return capture;
        }
        finally
        {
            CaptureScope.Current = null;
        }
    }

    /// <summary>
    /// Typed-envelope dispatch — used by generated harness wrappers. Skips
    /// both URL routing and the deserializer stack; body/query slots are
    /// served pre-typed.
    /// </summary>
    public Task<ResponseBranchCapture> DispatchMethodAsync(
        MethodInfo method,
        IReadOnlyDictionary<string, object>? body = null,
        IReadOnlyDictionary<string, object>? query = null,
        IReadOnlyDictionary<string, object>? overrides = null)
    {
        var request = BuildRequest(method, query);
        return DispatchMethodAsync(method, request, body, query, overrides);
    }

    public async Task<ResponseBranchCapture> DispatchMethodAsync(
        MethodInfo method,
        IHttpRequest request,
        IReadOnlyDictionary<string, object>? body = null,
        IReadOnlyDictionary<string, object>? query = null,
        IReadOnlyDictionary<string, object>? overrides = null)
    {
        var candidate = CandidateFor(method);
        var envelope = new TypedRequestEnvelope(body, query, overrides);

        var capture = new ResponseBranchCapture();
        CaptureScope.Current = capture;
        try
        {
            var app = this.Application;
            var handlers = ApplicationHandlers.For(app);
            if (MethodDispatcherV3.ShouldDispatch(method))
            {
                var v3Matches = MethodDispatcherV3.BuildMatches(envelope, request,
                    new[] { candidate });
                await MethodDispatcherV3.DispatchAsync(app, handlers, request, v3Matches);
                return capture;
            }

            var matches = MethodDispatcher.BuildMatches(envelope, new[] { candidate });
            await MethodDispatcher.DispatchAsync(app, handlers, request, matches);
            return capture;
        }
        finally
        {
            CaptureScope.Current = null;
        }
    }

    private static RouteCandidate CandidateFor(MethodInfo method)
    {
        var controllerType = method.DeclaringType
            ?? throw new ArgumentException("method has no DeclaringType", nameof(method));
        var fvc = controllerType.GetCustomAttribute<FunctionViewControllerAttribute>(inherit: true)
            ?? throw new InvalidOperationException(
                $"{controllerType.FullName} is not decorated with [FunctionViewController].");

        var emptyCaptures = (IReadOnlyDictionary<string, string>)
            new Dictionary<string, string>(StringComparer.Ordinal);
        return new RouteCandidate(
            controllerType: controllerType,
            invokeResource: fvc,
            method: method,
            template: null!,
            captures: emptyCaptures);
    }

    /// <summary>
    /// Build a JSON-bodied request for <paramref name="method"/>: verb from
    /// its HttpVerbAttribute, path from its controller's route metadata,
    /// UTF-8 <paramref name="json"/> as the application/json content.
    /// </summary>
    public static HttpRequest BuildJsonRequest(MethodInfo method, string json,
        IReadOnlyDictionary<string, object>? query = null)
    {
        var request = BuildRequest(method, query);
        var bytes = Encoding.UTF8.GetBytes(json);
        request.Content = bytes;
        request.Headers["Content-Type"] = new[] { "application/json" };
        // Envelope selection advertises the body via headers, as HTTP does
        request.Headers["Content-Length"] = new[] { bytes.Length.ToString() };
        return request;
    }

    private static HttpRequest BuildRequest(MethodInfo method,
        IReadOnlyDictionary<string, object>? query)
    {
        var controllerType = method.DeclaringType
            ?? throw new ArgumentException("method has no DeclaringType", nameof(method));

        var verbAttr = method.GetCustomAttributes(inherit: true)
            .OfType<HttpVerbAttribute>()
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"{controllerType.FullName}.{method.Name} has no HttpVerbAttribute " +
                "(e.g. [HttpGet], [HttpPost], [HttpPatch]) — cannot infer request verb.");

        var fvc = controllerType.GetCustomAttribute<FunctionViewControllerAttribute>(inherit: true)
            ?? throw new InvalidOperationException(
                $"{controllerType.FullName} is not decorated with [FunctionViewController].");

        var ns = string.IsNullOrWhiteSpace(fvc.Namespace) ? "api" : fvc.Namespace;
        var route = !string.IsNullOrWhiteSpace(fvc.Route) ? fvc.Route : controllerType.Name;

        var path = $"/{ns}/{route}";
        var queryString = (query is null || query.Count == 0)
            ? string.Empty
            : "?" + string.Join("&", query.Select(kvp =>
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(StringifyQueryValue(kvp.Value))}"));

        var url = $"http://localhost{path}{queryString}";
        return new HttpRequest(new Uri(url))
        {
            Method = new HttpMethod(verbAttr.Method),
        };
    }

    private static string StringifyQueryValue(object? value) => value switch
    {
        null => string.Empty,
        EastFive.IReferenceable reference => reference.id.ToString(),
        _ => value.ToString() ?? string.Empty,
    };
}
