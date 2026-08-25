using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using EastFive;

namespace EastFive.Reflection
{
    /// <summary>
    /// Composable, lazy attribute-interface scanning.
    /// </summary>
    /// <remarks>
    /// Each helper yields the attribute-interface implementations declared at a
    /// single lexical location — parameter, method, type, assembly, or the whole
    /// loaded domain. The calling method <c>Concat</c>s the helpers in whatever
    /// precedence order it wants, producing one lazy <see cref="IEnumerable{T}"/>,
    /// then chooses the resolution strategy with plain LINQ:
    /// <list type="bullet">
    /// <item><description><c>.First(...)</c> / <c>.FirstOrDefault()</c> — first-in-scope wins.</description></item>
    /// <item><description>iterate / <c>.ToArray()</c> — collect every matching attribute.</description></item>
    /// </list>
    /// This is pure reflection — no I/O — so it is safe on the critical path.
    /// Deferred execution means later locations are never scanned when an earlier
    /// one already satisfies the consumer (e.g. a first-wins <c>.First()</c> that
    /// matches on the parameter never touches the assembly or domain). The heavier
    /// type/assembly/domain scans are cached per <c>(scope, interface, inherit, multiple)</c>;
    /// the cheap parameter/method scans are read directly.
    /// </remarks>
    public static class AttributeInterfaceScope
    {
        private static readonly ConcurrentDictionary<(object scope, Type iface, bool inherit, bool multiple), object> cache
            = new ConcurrentDictionary<(object, Type, bool, bool), object>();

        private static readonly object domainScopeKey = new object();

        private static T[] Cached<T>(object scope, bool inherit, bool multiple, Func<T[]> compute)
            => (T[])cache.GetOrAdd((scope, typeof(T), inherit, multiple), _ => compute());

        /// <summary>
        /// Attribute interfaces declared directly on <paramref name="parameter"/>.
        /// Lazy; safe to call on a null parameter (yields nothing).
        /// </summary>
        public static IEnumerable<T> AttributeInterfacesInParameter<T>(this ParameterInfo parameter,
            bool inherit = false)
        {
            if (parameter == null)
                yield break;
            foreach (var attr in parameter.GetAttributesInterface<T>(inherit))
                yield return attr;
        }

        /// <summary>
        /// Attribute interfaces declared on <paramref name="method"/>.
        /// Lazy; safe to call on a null method (yields nothing).
        /// </summary>
        public static IEnumerable<T> AttributeInterfacesInMethod<T>(this MethodInfo method,
            bool inherit = false)
        {
            if (method == null)
                yield break;
            foreach (var attr in method.GetAttributesInterface<T>(inherit))
                yield return attr;
        }

        /// <summary>
        /// Attribute interfaces declared on <paramref name="type"/>. When
        /// <paramref name="multiple"/> is set the base-type chain is walked too.
        /// Lazy and cached; safe to call on a null type (yields nothing).
        /// </summary>
        public static IEnumerable<T> AttributeInterfacesInType<T>(this Type type,
            bool inherit = false, bool multiple = false)
        {
            if (type == null)
                yield break;
            foreach (var attr in Cached(type, inherit, multiple,
                () => type.GetAttributesInterface<T>(inherit, multiple)))
                yield return attr;
        }

        /// <summary>
        /// Attribute interfaces declared at the assembly level (e.g. <c>[assembly: Foo]</c>).
        /// Lazy and cached; safe to call on a null assembly (yields nothing).
        /// </summary>
        public static IEnumerable<T> AttributeInterfacesInAssembly<T>(this Assembly assembly)
        {
            if (assembly == null)
                yield break;
            foreach (var attr in Cached(assembly, false, false,
                () => assembly.GetAttributesInterface<T>()))
                yield return attr;
        }

        /// <summary>
        /// Assembly-level attribute interfaces across every currently loaded
        /// assembly in the app domain. Lazy and cached. Assemblies that fail to
        /// reflect are skipped.
        /// </summary>
        public static IEnumerable<T> AttributeInterfacesInDomain<T>()
        {
            foreach (var attr in Cached<T>(domainScopeKey, false, false, ComputeDomain<T>))
                yield return attr;
        }

        private static T[] ComputeDomain<T>()
            => AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly =>
                {
                    try
                    {
                        return assembly.GetAttributesInterface<T>();
                    }
                    catch (Exception)
                    {
                        return Array.Empty<T>();
                    }
                })
                .ToArray();

        /// <summary>
        /// The common default precedence chain for a parameter:
        /// parameter → method → declaring type (inherited, base-walked) → assembly.
        /// Callers that need a different order should <c>Concat</c> the per-location
        /// helpers themselves.
        /// </summary>
        public static IEnumerable<T> AttributeInterfacesInScope<T>(this ParameterInfo parameter,
            bool inherit = false)
        {
            var method = parameter?.Member as MethodInfo;
            var declaringType = method?.DeclaringType;
            return parameter.AttributeInterfacesInParameter<T>(inherit)
                .Concat(method.AttributeInterfacesInMethod<T>(inherit))
                .Concat(declaringType.AttributeInterfacesInType<T>(inherit: true, multiple: true))
                .Concat((declaringType?.Assembly).AttributeInterfacesInAssembly<T>());
        }
    }
}
