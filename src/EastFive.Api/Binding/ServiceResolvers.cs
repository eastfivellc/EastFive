using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;

namespace EastFive.Api.Binding
{
    /// <summary>
    /// Static, explicit registry of <see cref="IProvideService"/> resolvers used by
    /// the V3 dispatcher to satisfy method parameters that do not carry an
    /// <see cref="IBindFromRequest"/> attribute. Apps register resolvers once at
    /// startup; no attribute reflection is involved.
    /// <para>
    /// Lookup is exact-type for now (no inheritance walking); register a resolver
    /// per service type. Re-registering the same type replaces the prior resolver
    /// — useful for tests that swap implementations.
    /// </para>
    /// </summary>
    public static class ServiceResolvers
    {
        private static readonly ConcurrentDictionary<Type, IProvideService> resolvers =
            new ConcurrentDictionary<Type, IProvideService>();

        /// <summary>
        /// Register a resolver for parameters of type <typeparamref name="T"/>.
        /// </summary>
        public static void Register<T>(
            Func<IApplication, IHttpRequest, ParameterInfo, ValueTask<T>> resolver)
        {
            if (resolver is null) throw new ArgumentNullException(nameof(resolver));
            resolvers[typeof(T)] = new DelegateResolver<T>(resolver);
        }

        /// <summary>
        /// Lookup the resolver for the given parameter type, if any was registered.
        /// </summary>
        public static bool TryGet(Type serviceType, out IProvideService resolver) =>
            resolvers.TryGetValue(serviceType, out resolver);

        /// <summary>
        /// Drop all registrations — primarily for test isolation.
        /// </summary>
        public static void Clear() => resolvers.Clear();

        private sealed class DelegateResolver<T> : IProvideService
        {
            private readonly Func<IApplication, IHttpRequest, ParameterInfo, ValueTask<T>> resolver;

            public DelegateResolver(Func<IApplication, IHttpRequest, ParameterInfo, ValueTask<T>> resolver)
            {
                this.resolver = resolver;
            }

            public Type ServiceType => typeof(T);

            public async ValueTask<object> ResolveAsync(IApplication app, IHttpRequest request,
                ParameterInfo parameter)
            {
                return await resolver(app, request, parameter);
            }
        }
    }
}
