using System.Collections.Generic;
using System.Runtime.CompilerServices;

using EastFive.Reflection;

namespace EastFive.Api.Routing
{
    /// <summary>
    /// Cached, pre-resolved snapshot of every app-class attribute-interface
    /// the dispatch pipeline consumes per request. Replaces the
    /// <c>httpApp.GetType().GetAttributesInterface&lt;T&gt;(true, true)</c>
    /// reflection sweeps inside <see cref="MethodDispatcher"/>.
    ///
    /// The contents are stable for the application's lifetime (attributes
    /// don't move) so this can be safely cached forever via
    /// <see cref="ApplicationHandlers.For"/>.
    /// </summary>
    public interface IApplicationHandlers
    {
        IReadOnlyList<IDeserializeRequestEnvelope> Deserializers { get; }

        IReadOnlyList<IHandleRoutes> RouteHandlers { get; }

        IReadOnlyList<IHandleMethodInvocation> AppLevelInvocationHandlers { get; }

        IReadOnlyList<IHandleExceptions> ExceptionHandlers { get; }
    }

    /// <summary>
    /// Default <see cref="IApplicationHandlers"/> built by reflecting the
    /// application's runtime type for each consumed attribute interface.
    /// Cached per <see cref="IApplication"/> instance via
    /// <see cref="ConditionalWeakTable{TKey,TValue}"/>, mirroring
    /// <c>RouteTable.For</c>.
    /// </summary>
    public sealed class ApplicationHandlers : IApplicationHandlers
    {
        private static readonly ConditionalWeakTable<IApplication, ApplicationHandlers> cache
            = new ConditionalWeakTable<IApplication, ApplicationHandlers>();

        public static IApplicationHandlers For(IApplication application)
            => cache.GetValue(application, app => new ApplicationHandlers(app));

        public IReadOnlyList<IDeserializeRequestEnvelope> Deserializers { get; }

        public IReadOnlyList<IHandleRoutes> RouteHandlers { get; }

        public IReadOnlyList<IHandleMethodInvocation> AppLevelInvocationHandlers { get; }

        public IReadOnlyList<IHandleExceptions> ExceptionHandlers { get; }

        private ApplicationHandlers(IApplication application)
        {
            var appType = application.GetType();
            this.Deserializers = appType
                .GetAttributesInterface<IDeserializeRequestEnvelope>(true, true);
            this.RouteHandlers = appType
                .GetAttributesInterface<IHandleRoutes>(true, true);
            this.AppLevelInvocationHandlers = appType
                .GetAttributesInterface<IHandleMethodInvocation>(true, true);
            this.ExceptionHandlers = appType
                .GetAttributesInterface<IHandleExceptions>(true, true);
        }
    }
}
