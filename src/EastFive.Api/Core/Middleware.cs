using EastFive.Api.Modules;
using EastFive.Api.Services;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Razor;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EastFive.Api.Core
{
    public class Middleware : IAsyncDisposable
    {
        private readonly RequestDelegate continueAsync;
        private readonly IApplication app;
        private readonly IRazorViewEngine razorViewEngine;
        private readonly string[] pathLookups;
        private readonly IBackgroundTaskQueue taskQueue;

        public const string HeaderStatusType = "X-StatusType";
        public const string HeaderStatusName = "X-StatusName";

        public Middleware(RequestDelegate next, IApplication app,
            IRazorViewEngine razorViewEngine,
            IBackgroundTaskQueue taskQueue)
        {
            this.continueAsync = next;
            this.app = app;
            this.razorViewEngine = razorViewEngine;
            this.taskQueue = taskQueue;
            this.pathLookups = app.Resources
                .Select(res => res.invokeResourceAttr.Namespace)
                .Where(res => res.HasBlackSpace())
                .Distinct()
                .ToArray();
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                var matchesResources = pathLookups.Any(pathLookup => context.Request.Path.StartsWithSegments('/' + pathLookup));
                if (!matchesResources)
                {
                    await continueAsync(context);
                    return;
                }

                var requestLifetime = context.Features.Get<IHttpRequestLifetimeFeature>();
                var cancellationToken = requestLifetime.IsDefaultOrNull() ?
                    new CancellationToken()
                    :
                    requestLifetime.RequestAborted;
                context.Request.EnableBuffering();
                var request = new CoreHttpRequest(context.Request, this.razorViewEngine, cancellationToken);
                var routeResponse = await InvokeRequestAsync(request, this.app,
                    () =>
                    {
                        return new HttpResponse(context, continueAsync);
                    });

                await routeResponse.WriteResponseAsync(context);

                if (routeResponse is IHaveMoreWork)
                    taskQueue.QueueBackgroundWorkItem(
                        (cancellationToken) => (routeResponse as IHaveMoreWork)
                            .ProcessWorkAsync(cancellationToken));
            } catch(Exception ex)
            {
                try
                {
                    context.Response.StatusCode = 500;
                } catch(Exception)
                {

                }

                var stackTraceBytes = $"{ex.Message}\n\n{ex.StackTrace}".GetBytes();
                await context.Response.Body.WriteAsync(stackTraceBytes, 0, stackTraceBytes.Length);
            }
        }

        private class HttpResponse : IHttpResponse
        {
            private readonly RequestDelegate continueAsync;
            private readonly HttpContext context;

            public HttpResponse(HttpContext context, RequestDelegate continueAsync)
            {
                this.context = context;
                this.continueAsync = continueAsync;
            }

            public IHttpRequest Request => throw new NotImplementedException();

            public HttpStatusCode StatusCode { get; set; }

            public string ReasonPhrase 
            {
                get => default; 
                set => throw new NotImplementedException(); 
            }

            public IDictionary<string, string[]> Headers => new Dictionary<string, string[]>();

            public HttpResponse(RequestDelegate continueAsync)
            {
                this.continueAsync = continueAsync;
            }

            public void AddCookie(string cookieKey, string cookieValue, TimeSpan? expireTime)
            {
                CookieOptions option = new CookieOptions()
                {
                    Secure = true,
                    HttpOnly = true,
                };

                if (expireTime.HasValue)
                    option.Expires = DateTime.Now + expireTime.Value;
                else
                    option.Expires = DateTime.Now.AddMilliseconds(10);

                context.Response.Cookies.Append(cookieKey, cookieValue, option);
            }

            public Task WriteResponseAsync(HttpContext context)
            {
                return continueAsync(this.context);
            }

            public void WritePreamble(HttpContext context)
            {
                // Cookies Written on the fly above
            }

            public Task WriteResponseAsync(Stream stream)
            {
                throw new NotImplementedException();
            }
        }

        public static async Task<IHttpResponse> InvokeRequestAsync(IHttpRequest requestMessage,
            IApplication application,
            Func<IHttpResponse> skip)
        {
            // TODO: Discover the Method Dispatcher via attribute interface pattern.

            // Method-based routing — the middleware owns dispatch end-to-end.
            //
            //   1. Flat candidate list against the app-lifetime RouteTable
            //      (path/verb match — no reflection, no regex compilation).
            //   2. Pick the deserializer once, build the envelope once.
            //   3. Per-candidate RouteEnvelope (envelope + that candidate's
            //      regex captures) drives BindingRequirement fulfillment.
            //   4. IHandleRoutes wraps the chosen method's bind+invoke step,
            //      using the controller the chosen method actually belongs to.
            //
            // FunctionViewControllerAttribute is no longer in the dispatch loop;
            // it just contributes route/namespace defaults via IInvokeResource.
            var candidates = RouteTable.For(application).Match(requestMessage);

            if (candidates.Length == 0)
                return skip();

            var handlers = Routing.ApplicationHandlers.For(application);
            return await Routing.MethodDispatcher.PickDeserializerAsync(handlers, requestMessage,
                async (envelope) =>
                {
                    // Phase 7 fork: partition candidates by which dispatcher they
                    // opt into. A method is V3 iff any of its parameters carries
                    // an attribute implementing IBindFromRequest; everything else
                    // stays on the legacy v2 path. V3 candidates dispatch first;
                    // only if V3 produces no matches do we fall through to V2
                    // (so a V2 fallback endpoint at the same route can still
                    // catch a request that V3 candidates all reject).
                    var v3Candidates = candidates
                        .Where(c => Binding.MethodDispatcherV3.ShouldDispatch(c.Method))
                        .ToArray();
                    var v2Candidates = v3Candidates.Length == candidates.Length
                        ? Array.Empty<Routing.RouteCandidate>()
                        : candidates
                            .Where(c => !Binding.MethodDispatcherV3.ShouldDispatch(c.Method))
                            .ToArray();

                    if (v3Candidates.Length > 0)
                    {
                        var v3Matches = Binding.MethodDispatcherV3
                            .BuildMatches(envelope, requestMessage, v3Candidates);
                        if (v3Matches.Length > 0)
                            return await Binding.MethodDispatcherV3
                                .DispatchAsync(application, handlers, requestMessage, v3Matches);
                        if (v2Candidates.Length == 0)
                            return await Binding.MethodDispatcherV3
                                .DispatchAsync(application, handlers, requestMessage, v3Matches);
                    }

                    var matches = Routing.MethodDispatcher
                        .BuildMatches(envelope, v2Candidates.Length > 0 ? v2Candidates : candidates);

                    return await Routing.MethodDispatcher
                        .DispatchAsync(application, handlers, requestMessage, matches);
                });
        }

        ConcurrentQueue<IAsyncDisposable> asyncDisposables = new ConcurrentQueue<IAsyncDisposable>();

        public async ValueTask DisposeAsync()
        {
            while (asyncDisposables.TryDequeue(out IAsyncDisposable disposable))
                await disposable.DisposeAsync();
        }
    }
}
