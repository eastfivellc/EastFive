using System;
using System.Collections.Generic;
using System.Threading;

using EastFive.Serialization.Binding;

namespace EastFive.Api.Binding
{
    /// <summary>
    /// Application-wide registry of additional <see cref="ITypeBinder"/>s that
    /// <see cref="MethodDispatcherV3"/> layers on top of
    /// <see cref="TypeBindings.Default"/>.
    /// <para>
    /// <see cref="TypeBindings.Default"/> is immutable by design — tests rely on
    /// its deterministic contents. To contribute binders for app-specific
    /// shapes (e.g. <c>StorableEntity&lt;T&gt;</c>, <c>StorageEntity&lt;T&gt;</c>,
    /// <c>IQueryable&lt;T&gt;</c>), call <see cref="Register"/> from a module
    /// initializer in the contributing assembly. The V3 dispatcher snapshots the
    /// list once and prepends each binder via <see cref="ITypeBindings.With"/> so
    /// later registrations win.
    /// </para>
    /// <para>
    /// This is intentionally a thin static — registration happens at process
    /// start, never at request time. A future refactor can replace it with a
    /// proper DI-scoped binding context.
    /// </para>
    /// </summary>
    public static class TypeBinderRegistry
    {
        private static readonly List<ITypeBinder> registered = new();
        private static int version;

        /// <summary>
        /// Register a binder. Safe to call from <c>[ModuleInitializer]</c> or
        /// composition root. Idempotent for the same instance (does not check
        /// equality on equivalent instances — callers should register once).
        /// </summary>
        public static void Register(ITypeBinder binder)
        {
            if (binder is null) throw new ArgumentNullException(nameof(binder));
            lock (registered)
            {
                registered.Add(binder);
                Interlocked.Increment(ref version);
            }
        }

        /// <summary>
        /// Snapshot of currently-registered binders, in registration order.
        /// </summary>
        public static ITypeBinder[] Snapshot()
        {
            lock (registered)
            {
                return registered.ToArray();
            }
        }

        /// <summary>
        /// Monotonic counter that increments on every <see cref="Register"/>
        /// call. Consumers can cache derived state and invalidate when this
        /// changes.
        /// </summary>
        public static int Version => Volatile.Read(ref version);

        /// <summary>
        /// Build an <see cref="ITypeBindings"/> equal to <paramref name="baseline"/>
        /// with every registered binder prepended (last-registered binds first).
        /// </summary>
        public static ITypeBindings ApplyTo(ITypeBindings baseline)
        {
            if (baseline is null) throw new ArgumentNullException(nameof(baseline));
            var snapshot = Snapshot();
            var result = baseline;
            for (var i = 0; i < snapshot.Length; i++)
                result = result.With(snapshot[i]);
            return result;
        }
    }
}
