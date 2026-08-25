using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

using EastFive.Api.Core;
using EastFive.Api.Routing;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Reflection;
using EastFive.Serialization.Binding;

namespace EastFive.Api.Binding
{
    /// <summary>
    /// V3 dispatch pipeline: parallel to <see cref="MethodDispatcher"/> but driven
    /// by <see cref="IBindFromRequest"/> selection attributes and a single
    /// <see cref="CompositeBindingSource"/> entering the bind phase.
    /// <list type="number">
    ///   <item><see cref="ShouldDispatch"/> — does this method opt into V3?
    ///   (Any parameter with an <see cref="IBindFromRequest"/>-implementing
    ///   attribute. Phase 7 uses this to fork between v2 and v3.)</item>
    ///   <item><see cref="BuildMatches"/> — for each route candidate, ask every
    ///   selection attribute for its <see cref="BindCall"/>; drop the candidate
    ///   on any miss. Survivors compose into a <see cref="MethodMatchV3"/>.</item>
    ///   <item><see cref="DispatchAsync"/> — single-winner check (501/500), then
    ///   route-handler chain → <see cref="BindAndInvokeAsync"/>.</item>
    ///   <item><see cref="BindAndInvokeAsync"/> — per parameter:
    ///   <see cref="TypeBindings"/>.<c>Bind&lt;object&gt;</c> for bound members,
    ///   <see cref="ServiceResolvers"/>.<c>TryGet</c> for services; missing
    ///   service = 500. Then hand off to the existing
    ///   <see cref="MethodDispatcher.RunInvocationChainAsync"/>.</item>
    /// </list>
    /// The invocation chain (<see cref="IHandleMethodInvocation"/>) is reused
    /// verbatim — it only consumes <c>(parameter, value)</c> pairs and has no
    /// awareness of v2 vs v3.
    /// </summary>
    public static class MethodDispatcherV3
    {
        private static int defaultsRegistered;

        private static ITypeBindings cachedBindings;
        private static int cachedBindingsVersion = -1;

        private static readonly IReadOnlyDictionary<ParameterInfo, object> EmptyContexts =
            new Dictionary<ParameterInfo, object>(0);

        private static readonly IReadOnlyDictionary<string, string[]> EmptyQuery =
            new Dictionary<string, string[]>(0);

        /// <summary>
        /// Per-method cache of the URL query keys claimed across all of a method's
        /// selection parameters. The claimed set is a property of the method
        /// signature (not of any one request), so it is computed once and reused.
        /// </summary>
        private static readonly ConcurrentDictionary<MethodInfo, HashSet<string>>
            consumedQueryKeysByMethod = new();

        /// <summary>
        /// True when the method has at least one parameter carrying an attribute
        /// that implements <see cref="IBindFromRequest"/>. The router uses this
        /// in Phase 7 to fork V3-opted endpoints away from
        /// <see cref="MethodDispatcher"/>.
        /// </summary>
        public static bool ShouldDispatch(MethodInfo method)
        {
            foreach (var p in method.GetParameters())
            {
                if (p.TryGetAttributeInterface<IBindFromRequest>(out _))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// For each <see cref="RouteCandidate"/>, ask every
        /// <see cref="IBindFromRequest"/>-attributed parameter to contribute a
        /// <see cref="BindCall"/>. Any miss disqualifies that candidate; survivors
        /// produce a <see cref="MethodMatchV3"/> whose source is keyed by parameter
        /// name. Non-bound parameters (services) are not consulted here — they are
        /// resolved at bind time via <see cref="ServiceResolvers"/>.
        /// </summary>
        public static MethodMatchV3[] BuildMatches(IRequestEnvelope envelope,
            IHttpRequest request, RouteCandidate[] routeMatches)
        {
            var body = envelope as IRequestEnvelopeBody;
            var overrides = envelope as IParameterOverrideSource;
            var query = ParseQueryMulti(request);
            var results = new List<MethodMatchV3>(routeMatches.Length);
            foreach (var rc in routeMatches)
            {
                if (TryBuildMatch(body, query, request, rc, overrides, out var match))
                    results.Add(match);
            }
            return results.ToArray();
        }

        private static bool TryBuildMatch(IRequestEnvelopeBody body,
            IReadOnlyDictionary<string, string[]> query, IHttpRequest request,
            RouteCandidate rc, IParameterOverrideSource overrides, out MethodMatchV3 match)
        {
            var method = rc.Method.IsGenericMethod
                ? rc.Method.MakeGenericMethod(rc.ControllerType.AsArray())
                : rc.Method;
            var env = new RequestEnvelopeV3(body, query, rc.Captures, request);
            var members = new Dictionary<string, BindCall>(StringComparer.Ordinal);
            foreach (var p in method.GetParameters())
            {
                if (!p.TryGetAttributeInterface<IBindFromRequest>(out var binder))
                    continue;
                // An override pre-empts selection: the test has the typed
                // value already, so the IBindFromRequest selection ladder
                // doesn't get to vote and can't reject the match for a
                // body/query value it wouldn't have found anyway. The
                // parameter is intentionally NOT added to the composite
                // source — BindAndInvokeAsync consults `Overrides` first
                // and supplies the value directly.
                if (overrides is not null
                    && overrides.TryGetParameterOverride(p.Name, out _))
                    continue;
                if (!binder.TrySelectSource(env, p, out var call))
                {
                    match = default;
                    return false;
                }
                members[p.Name] = call;
            }
            // V2 parity: a candidate matches only if every URL query key is claimed
            // by some parameter (required OR optional). The per-parameter selection
            // ladder above is positive-only — a data-free list endpoint (whose sole
            // bound parameter is e.g. [StorageEntities] IQueryable<T>) never inspects
            // the query string, so without this rejection it would shadow a keyed
            // by-id endpoint sharing the same route + verb whenever `?id=` is present.
            // Methods opting out via [HttpX(MatchAllParameters = false)] parse the
            // query themselves (e.g. OAuth callback controllers) and are exempt.
            if (query.Count > 0 && MatchesAllQueryParameters(method))
            {
                var consumed = ConsumedQueryKeysFor(method);
                foreach (var key in query.Keys)
                {
                    if (!consumed.Contains(key))
                    {
                        match = default;
                        return false;
                    }
                }
            }
            match = new MethodMatchV3(rc.ControllerType, rc.InvokeResource, method,
                new CompositeBindingSource(members), overrides);
            return true;
        }

        /// <summary>
        /// Whether the method demands that every URL query key be consumed by a
        /// parameter. False when its HTTP verb attribute opts out via
        /// <c>MatchAllParameters = false</c> / <c>MatchAllQueryParameters = false</c>
        /// (such methods parse the query string themselves).
        /// </summary>
        internal static bool MatchesAllQueryParameters(MethodInfo method)
        {
            var verbAttr = method.GetCustomAttribute<HttpVerbAttribute>();
            if (verbAttr is not null)
                return verbAttr.MatchAllQueryParameters;
            return true;
        }

        /// <summary>
        /// The set of URL query keys a method's parameters claim, unioned across
        /// every <see cref="IBindFromRequest"/> selection attribute (via
        /// <see cref="IBindFromRequest.GetConsumedQueryKeys"/>) and every legacy
        /// <see cref="IBindApiValue"/> attribute (so a V3-dispatched method that
        /// also carries a V2 query parameter is not falsely rejected). Cached per
        /// method; comparison is case-insensitive to match query parsing.
        /// <para>
        /// Exposed to the V2 <see cref="EastFive.Api.Routing.MethodDispatcher"/>
        /// so both dispatchers reject candidates that leave a URL query key
        /// unconsumed using identical semantics.
        /// </para>
        /// </summary>
        internal static HashSet<string> ConsumedQueryKeysFor(MethodInfo method)
        {
            return consumedQueryKeysByMethod.GetOrAdd(method, m =>
            {
                var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in m.GetParameters())
                {
                    if (p.TryGetAttributeInterface<IBindFromRequest>(out var binder))
                        foreach (var key in binder.GetConsumedQueryKeys(p))
                            if (key.HasBlackSpace())
                                keys.Add(key);
                    if (p.TryGetAttributeInterface<IBindApiValue>(out var v2Binder))
                    {
                        var key = v2Binder.GetKey(p);
                        if (key.HasBlackSpace())
                            keys.Add(key);
                    }
                }
                return keys;
            });
        }

        /// <summary>
        /// Pick the single valid V3 match (501 when none, 500 when ambiguous),
        /// wrap the chosen one in the application's
        /// <see cref="IApplicationHandlers.RouteHandlers"/> chain, and hand off
        /// to <see cref="BindAndInvokeAsync"/>.
        /// </summary>
        public static Task<IHttpResponse> DispatchAsync(IApplication httpApp,
            IApplicationHandlers handlers, IHttpRequest request, MethodMatchV3[] matches)
        {
            return matches
                .Single(
                    onNone: () => request
                        .CreateResponse(HttpStatusCode.NotImplemented)
                        .AddReason("No V3 method matched the request")
                        .AsTask(),
                    onSingle: chosen => InvokeChosenAsync(httpApp, handlers, request, chosen),
                    onMultiple: many => request
                        .CreateResponse(HttpStatusCode.InternalServerError)
                        .AddReason("Ambiguous V3 method match: " + many
                            .Select(m => $"{m.Method.DeclaringType?.Name}.{m.Method.Name}").Join(", "))
                        .AsTask());
        }

        private static Task<IHttpResponse> InvokeChosenAsync(IApplication httpApp,
            IApplicationHandlers handlers, IHttpRequest request, MethodMatchV3 chosen)
        {
            return handlers.RouteHandlers
                .Aggregate<IHandleRoutes, RouteHandlingDelegate>(
                    (ctrlFinal, appFinal, reqFinal) =>
                        BindAndInvokeAsync(appFinal, handlers, reqFinal, chosen),
                    (callback, routeHandler) =>
                        (ctrlCur, appCur, reqCur) =>
                            routeHandler.HandleRouteAsync(ctrlCur, chosen.InvokeResource,
                                appCur, reqCur, callback))
                .Invoke(chosen.ControllerType, httpApp, request);
        }

        /// <summary>
        /// Bind every parameter — selection-bound members via
        /// <see cref="TypeBindings.Default"/>, service parameters via
        /// <see cref="ServiceResolvers"/> — then dispatch through the existing
        /// <see cref="IHandleMethodInvocation"/> chain.
        /// </summary>
        public static async Task<IHttpResponse> BindAndInvokeAsync(IApplication httpApp,
            IApplicationHandlers handlers, IHttpRequest request, MethodMatchV3 chosen)
        {
            EnsureDefaults();

            var parameters = chosen.Method.GetParameters();
            var bindings = new List<KeyValuePair<ParameterInfo, object>>(parameters.Length);
            foreach (var p in parameters)
            {
                if (chosen.Overrides is not null
                    && chosen.Overrides.TryGetParameterOverride(p.Name, out var overrideValue))
                {
                    if (overrideValue is null
                        ? p.ParameterType.IsValueType && Nullable.GetUnderlyingType(p.ParameterType) is null
                        : !p.ParameterType.IsInstanceOfType(overrideValue))
                    {
                        return request
                            .CreateResponse(HttpStatusCode.BadRequest)
                            .AddReason(
                                $"Parameter override for `{p.Name}` " +
                                $"is `{overrideValue?.GetType().FullName ?? "null"}`, " +
                                $"not assignable to `{p.ParameterType.FullName}`.");
                    }
                    bindings.Add(new KeyValuePair<ParameterInfo, object>(p, overrideValue));
                    continue;
                }
                if (chosen.Source.HasMember(p.Name))
                {
                    var (ok, value, failureResponse) = await TryBindMemberAsync(p, chosen.Source, request, httpApp);
                    if (!ok)
                        return failureResponse;
                    bindings.Add(new KeyValuePair<ParameterInfo, object>(p, value));
                }
                else if (ServiceResolvers.TryGet(p.ParameterType, out var svc))
                {
                    var value = await svc.ResolveAsync(httpApp, request, p);
                    bindings.Add(new KeyValuePair<ParameterInfo, object>(p, value));
                }
                // Else: legacy-instigated parameter (response delegates carrying
                // HttpFuncDelegateAttribute, etc.). Leave it unbound here so
                // InvokeHandledMethodAsync's per-parameter ladder fills it via
                // httpApp.Instigate(...) — same path that already serves V2.
            }

            return await MethodDispatcher.RunInvocationChainAsync(httpApp, handlers, request,
                chosen.ControllerType, chosen.Method, bindings.ToArray(), EmptyContexts);
        }

        /// <summary>
        /// Bind a single member parameter via <see cref="TypeBindings.Default"/>.
        /// Optional parameters whose source contributed
        /// <see cref="BindCalls.NotPresent"/> fall through to the C# default value.
        /// Any other failure surfaces as a 400 response in the returned tuple.
        /// </summary>
        private static async ValueTask<(bool ok, object value, IHttpResponse failureResponse)>
            TryBindMemberAsync(ParameterInfo p, CompositeBindingSource source, IHttpRequest request, IApplication httpApp)
        {
            var bindings = GetBindings();
            var ctx = new ApiBindingContext(bindings, request, httpApp,
                slot: new ParameterSlot(p), keyPath: p.Name);
            // The IBindFromRequest selection attribute optionally declares the
            // member scope under which PocoBinder should walk complex types.
            if (p.TryGetAttributeInterface<IProvideMemberScope>(out var scopeProvider))
                ctx = (ApiBindingContext)ctx.WithMemberScope(scopeProvider.MemberScope);
            BindFailure? failure = null;
            // A reference type or Nullable<T> can accept an explicit null. Supplying
            // onNull realizes BindCalls.Null's documented intent: an absent
            // [QueryOptional]/[HeaderOptional] value binds to null WITHOUT requiring a
            // C# default. Value types without Nullable<> get no onNull, so an absent
            // optional there still falls through to the C# default (or fails NotPresent).
            var acceptsNull = !p.ParameterType.IsValueType
                || Nullable.GetUnderlyingType(p.ParameterType) is not null;
            var value = await bindings.Bind<object>(p.ParameterType, source, ctx,
                v => v,
                f => { failure = f; return null; },
                onNull: acceptsNull ? () => (object)null : null);
            if (failure is null)
                return (true, value, null);
            if (p.HasDefaultValue && failure.Value.Reason is NotPresent)
                return (true, p.DefaultValue, null);
            var statusCode = failure.Value.Reason is IBindFailureWithStatusCode coded
                ? coded.StatusCode
                : HttpStatusCode.BadRequest;
            var response = request
                .CreateResponse(statusCode)
                .AddReason($"Could not bind `{p.Name}`: {failure.Value}");
            return (false, null, response);
        }

        private static void EnsureDefaults()
        {
            if (Interlocked.Exchange(ref defaultsRegistered, 1) == 0)
                DefaultServiceResolvers.RegisterAll();
        }

        /// <summary>
        /// Lazily composes <see cref="TypeBindings.Default"/> with binders contributed
        /// via <see cref="TypeBinderRegistry"/>. Re-composed whenever the registry's
        /// version changes (i.e. when new binders register). The result is cached
        /// so steady-state dispatch incurs no per-request overlay cost.
        /// </summary>
        private static ITypeBindings GetBindings()
        {
            var v = TypeBinderRegistry.Version;
            var cached = Volatile.Read(ref cachedBindings);
            if (cached is not null && Volatile.Read(ref cachedBindingsVersion) == v)
                return cached;
            var fresh = TypeBinderRegistry.ApplyTo(TypeBindings.Default);
            Volatile.Write(ref cachedBindings, fresh);
            Volatile.Write(ref cachedBindingsVersion, v);
            return fresh;
        }

        private static IReadOnlyDictionary<string, string[]> ParseQueryMulti(IHttpRequest request)
        {
            var raw = request.RequestUri?.Query;
            if (string.IsNullOrEmpty(raw))
                return EmptyQuery;
            var parsed = HttpUtility.ParseQueryString(raw);
            var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in parsed.AllKeys)
            {
                if (key is null) continue;
                var values = parsed.GetValues(key);
                if (values is null) continue;
                result[key] = values;
            }
            return result;
        }
    }
}
