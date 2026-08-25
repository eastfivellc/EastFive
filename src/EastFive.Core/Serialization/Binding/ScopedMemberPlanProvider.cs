using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using EastFive.Linq;

namespace EastFive.Serialization.Binding
{
    /// <summary>
    /// Default <see cref="IMemberPlanProvider"/>: opt-in, scope-driven member
    /// discovery. A member is included in the plan for a given scope iff it
    /// carries an attribute implementing
    /// <see cref="IIncludeInMemberScope{TScope}"/> whose <c>Include</c> returns
    /// true. No convention fallback: a type with no scope-tagged members
    /// produces no iterations.
    ///
    /// <para>Plans are cached per <c>(targetType, scope)</c> on the singleton
    /// instance. The cached entries are <see cref="IMemberPlan"/> instances;
    /// the concrete <c>ReflectionMemberPlan</c> implementation is private to
    /// this provider — consumers depend only on the interface.</para>
    /// </summary>
    public sealed class ScopedMemberPlanProvider : IMemberPlanProvider
    {
        public static ScopedMemberPlanProvider Instance { get; } = new ScopedMemberPlanProvider();

        private readonly ConcurrentDictionary<(Type, Type), IMemberPlan[]> cache = new();

        public TResult Aggregate<TAccum, TResult>(
            Type targetType,
            Type scope,
            TAccum start,
            Func<TAccum, IMemberPlan, Func<TAccum, TResult>, TResult> aggr,
            Func<TAccum, TResult> onComplete)
        {
            if (targetType is null) throw new ArgumentNullException(nameof(targetType));
            if (scope is null) throw new ArgumentNullException(nameof(scope));
            if (!typeof(IMemberScope).IsAssignableFrom(scope))
                throw new ArgumentException(
                    $"Scope type {scope.FullName} must implement {nameof(IMemberScope)}.",
                    nameof(scope));
            if (aggr is null) throw new ArgumentNullException(nameof(aggr));
            if (onComplete is null) throw new ArgumentNullException(nameof(onComplete));

            var plan = cache.GetOrAdd((targetType, scope), key => BuildPlan(key.Item1, key.Item2));
            return plan.Aggregate(start, aggr, onComplete);
        }

        private static IMemberPlan[] BuildPlan(Type targetType, Type scope)
        {
            // Construct the closed generic IIncludeInMemberScope<TScope> we're
            // querying for, then call MemberInfo.GetCustomAttributes(inherit:true)
            // and select attributes whose runtime type implements it.
            var includeInterface = typeof(IIncludeInMemberScope<>).MakeGenericType(scope);

            IEnumerable<(MemberInfo m, object inc)> ConsideredMembers()
            {
                foreach (var p in targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!p.CanRead || !p.CanWrite) continue;
                    if (p.GetIndexParameters().Length != 0) continue;
                    if (TryFirstIncluder(p, includeInterface, out var inc))
                        yield return (p, inc);
                }
                foreach (var f in targetType.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (f.IsInitOnly || f.IsLiteral) continue;
                    if (TryFirstIncluder(f, includeInterface, out var inc))
                        yield return (f, inc);
                }
            }

            return ConsideredMembers()
                .Select(pair =>
                {
                    var (member, includer) = pair;
                    var wireName = (string)includeInterface
                        .GetMethod(nameof(IIncludeInMemberScope<DummyScope>.GetWireName))
                        .Invoke(includer, new object[] { member });
                    if (string.IsNullOrEmpty(wireName)) wireName = member.Name;

                    Func<object, object, object> withMember;
                    Func<object, object> read;
                    MemberSlot slot;
                    if (member is PropertyInfo pi)
                    {
                        // Reflection SetValue writes through the box for boxed
                        // structs, so returning inst is correct for both
                        // classes and structs.
                        withMember = (inst, v) => { pi.SetValue(inst, v); return inst; };
                        read = inst => pi.GetValue(inst);
                        slot = new MemberSlot(pi);
                    }
                    else
                    {
                        var fi = (FieldInfo)member;
                        withMember = (inst, v) => { fi.SetValue(inst, v); return inst; };
                        read = inst => fi.GetValue(inst);
                        slot = new MemberSlot(fi);
                    }
                    return (IMemberPlan)new ReflectionMemberPlan(slot, wireName, withMember, read);
                })
                .ToArray();
        }

        private static bool TryFirstIncluder(MemberInfo m, Type includeInterface, out object includer)
        {
            foreach (var a in m.GetCustomAttributes(inherit: true))
            {
                if (!includeInterface.IsAssignableFrom(a.GetType())) continue;
                var includeMethod = includeInterface
                    .GetMethod(nameof(IIncludeInMemberScope<DummyScope>.Include));
                var include = (bool)includeMethod.Invoke(a, new object[] { m });
                if (!include) continue;
                includer = a;
                return true;
            }
            includer = null;
            return false;
        }

        // Used only as a generic-type witness for nameof() on the open generic;
        // never instantiated, never reflected on at runtime.
        private sealed class DummyScope : IMemberScope { }

        /// <summary>
        /// Reflection-backed <see cref="IMemberPlan"/>. The sole implementation
        /// emitted by <see cref="ScopedMemberPlanProvider"/>. Private nested
        /// class — consumers depend only on <see cref="IMemberPlan"/>.
        /// </summary>
        private sealed class ReflectionMemberPlan : IMemberPlan
        {
            private readonly IBindingSlot slot;
            private readonly string wireName;
            private readonly Func<object, object, object> withMember;
            private readonly Func<object, object> read;

            public ReflectionMemberPlan(
                IBindingSlot slot,
                string wireName,
                Func<object, object, object> withMember,
                Func<object, object> read)
            {
                this.slot = slot ?? throw new ArgumentNullException(nameof(slot));
                this.wireName = wireName ?? throw new ArgumentNullException(nameof(wireName));
                this.withMember = withMember ?? throw new ArgumentNullException(nameof(withMember));
                this.read = read ?? throw new ArgumentNullException(nameof(read));
            }

            public Type MemberType => slot.Type;

            public IBindingContext ScopeInto(IBindingContext parent) =>
                parent.WithKeyPath(ComposePath(parent.KeyPath)).WithSlot(slot);

            public IBindingSink ScopeInto(IBindingSink parent) =>
                parent.Scope(wireName);

            public string ComposePath(string root) =>
                string.IsNullOrEmpty(root) ? wireName : $"{root}.{wireName}";

            public BindFailure DecorateFailure(BindFailure inner) =>
                new BindFailure(inner.Reason, inner.ExpectedType, wireName);

            public object Read(object instance) => read(instance);

            public object WithMember(object instance, object value) =>
                withMember(instance, value);
        }
    }
}
