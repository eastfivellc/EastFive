using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

using EastFive.Api.Routing;
using EastFive.Extensions;
using EastFive.Linq;

namespace EastFive.Api.Core
{
    /// <summary>
    /// Immutable, app-lifetime cache of the routing tuples
    /// <c>(controllerType, invokeResource, method, template)</c>.
    ///
    /// Built once per <see cref="IApplication"/> the first time it's queried
    /// (assembly-scan inputs — <see cref="IApplication.Resources"/>,
    /// <see cref="IApplication.GetExtensionMethods"/>, attribute reflection,
    /// <see cref="IMatchRoute.GetRouteTemplate"/>'s regex compilation — are
    /// all stable for an application's lifetime). Per-request work is then
    /// just a verb filter + <see cref="RouteTemplate.TryMatch"/> over the
    /// cached array, with no reflection or regex compilation on the hot path.
    /// </summary>
    public sealed class RouteTable
    {
        private static readonly ConditionalWeakTable<IApplication, RouteTable> cache
            = new ConditionalWeakTable<IApplication, RouteTable>();

        /// <summary>
        /// Lazily build (or return the cached) <see cref="RouteTable"/> for
        /// <paramref name="application"/>. Thread-safe: at most one table is
        /// constructed per application; concurrent first-callers race
        /// harmlessly (one wins, the loser's table is discarded).
        /// </summary>
        public static RouteTable For(IApplication application)
            => cache.GetValue(application, app => new RouteTable(app));

        private readonly RouteEntry[] entries;

        private RouteTable(IApplication application)
        {
            this.entries = application.Resources
                .NullToEmpty()
                .SelectMany(resource => BuildEntries(application, resource))
                .ToArray();
        }

        private static IEnumerable<RouteEntry> BuildEntries(IApplication application,
            ResourceInvocation resource)
        {
            var methods = resource.type
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Concat(application.GetExtensionMethods(resource.type))
                .Where(m => m.ContainsAttributeInterface<IMatchRoute>(true));

            foreach (var method in methods)
            {
                foreach (var matcher in method.GetAttributesInterface<IMatchRoute>())
                {
                    var template = matcher.GetRouteTemplate(method, resource.invokeResourceAttr);
                    if (template == null)
                        continue;
                    yield return new RouteEntry(resource.type, resource.invokeResourceAttr,
                        method, template);
                }
            }
        }

        /// <summary>Total number of cached entries — for diagnostics / tests.</summary>
        public int EntryCount => this.entries.Length;

        /// <summary>
        /// Per-request match: filter cached entries by verb, run
        /// <see cref="RouteTemplate.TryMatch"/>, and project to candidates.
        /// </summary>
        public RouteCandidate[] Match(IHttpRequest request)
        {
            var requestVerb = request.Method?.Method ?? string.Empty;
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;

            return this.entries
                .Select(entry => (entry,
                    matched: entry.Template.TryMatch(requestVerb, path, out var captures),
                    captures))
                .Where(t => t.matched)
                .Select(t => new RouteCandidate(
                    t.entry.ControllerType, t.entry.InvokeResource,
                    t.entry.Method, t.entry.Template, t.captures))
                .ToArray();
        }

        private readonly struct RouteEntry
        {
            public RouteEntry(Type controllerType, IInvokeResource invokeResource,
                MethodInfo method, RouteTemplate template)
            {
                this.ControllerType = controllerType;
                this.InvokeResource = invokeResource;
                this.Method = method;
                this.Template = template;
            }

            public Type ControllerType { get; }
            public IInvokeResource InvokeResource { get; }
            public MethodInfo Method { get; }
            public RouteTemplate Template { get; }
        }
    }
}
