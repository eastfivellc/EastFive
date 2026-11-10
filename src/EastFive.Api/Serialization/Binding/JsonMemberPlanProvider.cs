using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

using Newtonsoft.Json;

using EastFive.Api.Serialization.Json;
using EastFive.Linq;
using EastFive.Serialization.Binding;

namespace EastFive.Api.Serialization.Binding
{
    /// <summary>
    /// <see cref="IMemberPlanProvider"/> for <see cref="JsonBinding"/>: selects the
    /// same members, under the same wire names and in the same order, as Newtonsoft
    /// with <see cref="ApiPropertyContractResolver"/>.
    /// <para>
    /// <b>Governed</b> members — declared by a type whose assembly was
    /// <see cref="ApiPropertyContractResolver.RegisterManagedAssembly"/>'d AND which
    /// declares at least one member tagged for the scope — are included only when
    /// tagged for the scope (an attribute implementing
    /// <see cref="IIncludeInMemberScope{TScope}"/>, e.g. V3 <c>[ApiProperty]</c>),
    /// under the tag's wire name. <c>[JsonIgnore]</c> / <c>[JsonProperty]</c> do not
    /// apply to them. For the <c>ResponseBody</c> scope this is exactly the
    /// resolver's gate.
    /// </para>
    /// <para>
    /// <b>Ungoverned</b> members get Newtonsoft's default member set: public instance
    /// fields and properties plus non-public ones carrying <c>[JsonProperty]</c>,
    /// named by <c>[JsonProperty]</c> or the member name, skipping <c>[JsonIgnore]</c>
    /// (and everything without <c>[JsonProperty]</c> under
    /// <c>[JsonObject(MemberSerialization.OptIn)]</c>).
    /// </para>
    /// <para>
    /// Order is Newtonsoft's: fields, then properties, each in declaration order.
    /// Plans are cached per <c>(type, scope)</c>; register managed assemblies before
    /// the first bind.
    /// </para>
    /// </summary>
    public sealed class JsonMemberPlanProvider : IMemberPlanProvider
    {
        public static JsonMemberPlanProvider Instance { get; } = new JsonMemberPlanProvider();

        private readonly ConcurrentDictionary<(Type, Type), IMemberPlan[]> plans = new();
        private readonly ConcurrentDictionary<(Type, Type), bool> governed = new();

        private JsonMemberPlanProvider() { }

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
                    $"Scope type {scope.FullName} must implement {nameof(IMemberScope)}.", nameof(scope));
            if (aggr is null) throw new ArgumentNullException(nameof(aggr));
            if (onComplete is null) throw new ArgumentNullException(nameof(onComplete));

            var plan = plans.GetOrAdd((targetType, scope), key => BuildPlan(key.Item1, key.Item2));
            return plan.Aggregate(start, aggr, onComplete);
        }

        /// <summary>The members of <paramref name="targetType"/> under <paramref name="scope"/>, in plan order.</summary>
        internal IReadOnlyList<JsonMemberPlan> GetPlan(Type targetType, Type scope) =>
            plans.GetOrAdd((targetType, scope), key => BuildPlan(key.Item1, key.Item2))
                .Cast<JsonMemberPlan>()
                .ToArray();

        private IMemberPlan[] BuildPlan(Type targetType, Type scope)
        {
            var includeInterface = typeof(IIncludeInMemberScope<>).MakeGenericType(scope);
            var optIn = FirstAttribute<JsonObjectAttribute>(targetType)?.MemberSerialization
                == MemberSerialization.OptIn;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var candidates = targetType.GetFields(flags).Cast<MemberInfo>()
                .Concat(targetType.GetProperties(flags).Where(p => p.GetIndexParameters().Length == 0))
                .Where(m => !m.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
                .GroupBy(m => m.Name)
                .Select(g => g.First());

            var plan = new List<IMemberPlan>();
            foreach (var member in candidates)
            {
                var jsonProperty = FirstAttribute<JsonPropertyAttribute>(member);
                var isPublic = IsPublic(member);
                if (!isPublic && jsonProperty is null) continue;

                string wireName;
                if (IsGoverned(member.DeclaringType, scope, includeInterface))
                {
                    if (!isPublic) continue;
                    if (!TryGetIncluder(member, includeInterface, out var wire)) continue;
                    wireName = wire;
                }
                else
                {
                    if (optIn && jsonProperty is null) continue;
                    if (member.IsDefined(typeof(JsonIgnoreAttribute), inherit: true)) continue;
                    wireName = string.IsNullOrEmpty(jsonProperty?.PropertyName) ? member.Name : jsonProperty.PropertyName;
                }

                if (!TryCreate(member, wireName, out var memberPlan)) continue;
                plan.Add(memberPlan);
            }
            return plan.ToArray();
        }

        // EastFive.CustomAttributeExtensions shadows the BCL GetCustomAttribute<T>
        // (and throws when absent); this is the null-when-absent lookup.
        private static T FirstAttribute<T>(MemberInfo member) where T : Attribute =>
            (T)member.GetCustomAttributes(typeof(T), inherit: true).FirstOrDefault();

        private static bool IsPublic(MemberInfo member) => member switch
        {
            FieldInfo f => f.IsPublic,
            PropertyInfo p => p.GetGetMethod(nonPublic: false) is not null || p.GetSetMethod(nonPublic: false) is not null,
            _ => false,
        };

        private static bool TryCreate(MemberInfo member, string wireName, out IMemberPlan plan)
        {
            if (member is PropertyInfo p)
            {
                if (!p.CanRead || !p.CanWrite) { plan = null; return false; }
                plan = new JsonMemberPlan(member, new MemberSlot(p), wireName, p.PropertyType,
                    inst => p.GetValue(inst),
                    (inst, v) => { p.SetValue(inst, v); return inst; });
                return true;
            }
            var f = (FieldInfo)member;
            if (f.IsLiteral || f.IsInitOnly) { plan = null; return false; }
            plan = new JsonMemberPlan(member, new MemberSlot(f), wireName, f.FieldType,
                inst => f.GetValue(inst),
                (inst, v) => { f.SetValue(inst, v); return inst; });
            return true;
        }

        private bool IsGoverned(Type declaringType, Type scope, Type includeInterface)
        {
            if (declaringType is null) return false;
            return governed.GetOrAdd((declaringType, scope), key =>
            {
                var (type, _) = key;
                if (!ApiPropertyContractResolver.IsManagedAssembly(type.Assembly)) return false;
                const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
                return type.GetFields(flags).Cast<MemberInfo>()
                    .Concat(type.GetProperties(flags))
                    .Any(m => TryGetIncluder(m, includeInterface, out _));
            });
        }

        // Attribute-interface discovery: any attribute implementing
        // IIncludeInMemberScope<scope> whose Include(member) is true.
        private static bool TryGetIncluder(MemberInfo member, Type includeInterface, out string wireName)
        {
            foreach (var attribute in member.GetCustomAttributes(inherit: true))
            {
                if (!includeInterface.IsInstanceOfType(attribute)) continue;
                var include = (bool)includeInterface.GetMethod(nameof(IIncludeInMemberScope<IMemberScope>.Include))
                    .Invoke(attribute, new object[] { member });
                if (!include) continue;
                var name = (string)includeInterface.GetMethod(nameof(IIncludeInMemberScope<IMemberScope>.GetWireName))
                    .Invoke(attribute, new object[] { member });
                wireName = string.IsNullOrEmpty(name) ? member.Name : name;
                return true;
            }
            wireName = null;
            return false;
        }
    }

    /// <summary>One member of a <see cref="JsonMemberPlanProvider"/> plan.</summary>
    internal sealed class JsonMemberPlan : IMemberPlan
    {
        private readonly IBindingSlot slot;
        private readonly Func<object, object> read;
        private readonly Func<object, object, object> withMember;

        public JsonMemberPlan(
            MemberInfo member,
            IBindingSlot slot,
            string wireName,
            Type memberType,
            Func<object, object> read,
            Func<object, object, object> withMember)
        {
            Member = member;
            this.slot = slot;
            WireName = wireName;
            MemberType = memberType;
            this.read = read;
            this.withMember = withMember;
        }

        public MemberInfo Member { get; }

        public string WireName { get; }

        public Type MemberType { get; }

        public IBindingContext ScopeInto(IBindingContext parent) =>
            parent.WithKeyPath(ComposePath(parent.KeyPath)).WithSlot(slot);

        public IBindingSink ScopeInto(IBindingSink parent) => parent.Scope(WireName);

        public string ComposePath(string root) =>
            string.IsNullOrEmpty(root) ? WireName : $"{root}.{WireName}";

        /// <summary>
        /// Re-key <paramref name="inner"/> to this member, keeping a deeper path the
        /// inner binder already rooted here (<c>steps[3]</c>, <c>extra.key</c>), and
        /// naming this member's type when the source only knew "object".
        /// </summary>
        public BindFailure DecorateFailure(BindFailure inner)
        {
            var path = inner.KeyPath;
            var keepsPath = path == WireName ||
                path.StartsWith(WireName + "[", StringComparison.Ordinal) ||
                path.StartsWith(WireName + ".", StringComparison.Ordinal);
            var expected = inner.ExpectedType is null || inner.ExpectedType == typeof(object)
                ? MemberType
                : inner.ExpectedType;
            return new BindFailure(inner.Reason, expected, keepsPath ? path : WireName);
        }

        public object Read(object instance) => read(instance);

        public object WithMember(object instance, object value) => withMember(instance, value);
    }
}
