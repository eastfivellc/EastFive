using System.Threading;
using System.Threading.Tasks;

namespace EastFive.Api.Binding
{
    /// <summary>
    /// Bootstraps the built-in <see cref="ServiceResolvers"/> entries that every
    /// V3 endpoint needs: <see cref="IHttpRequest"/>, <see cref="IApplication"/>,
    /// and <see cref="CancellationToken"/>. The host calls
    /// <see cref="RegisterAll"/> once at startup (the V3 dispatcher will invoke
    /// it lazily on first use as a safety net). Re-invocation is safe —
    /// <see cref="ServiceResolvers.Register{T}"/> replaces any prior resolver.
    /// <para>
    /// Applications that need additional ambient services (telemetry, profilers,
    /// custom request scopes) should call their own
    /// <see cref="ServiceResolvers.Register{T}"/> after this method, or replace
    /// individual defaults by registering after this call.
    /// </para>
    /// </summary>
    public static class DefaultServiceResolvers
    {
        /// <summary>
        /// Registers the request, application, and cancellation-token resolvers.
        /// Idempotent.
        /// </summary>
        public static void RegisterAll()
        {
            ServiceResolvers.Register<IHttpRequest>((app, req, p) =>
                new ValueTask<IHttpRequest>(req));

            ServiceResolvers.Register<IApplication>((app, req, p) =>
                new ValueTask<IApplication>(app));

            ServiceResolvers.Register<CancellationToken>((app, req, p) =>
                new ValueTask<CancellationToken>(req.CancellationToken));
        }
    }
}
