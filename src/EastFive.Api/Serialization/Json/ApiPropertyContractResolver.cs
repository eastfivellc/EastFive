using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

using EastFive.Api.Binding.Scopes;
using EastFive.Serialization.Binding;

namespace EastFive.Api.Serialization.Json
{
    /// <summary>
    /// Newtonsoft <see cref="IContractResolver"/> that makes the V3
    /// <c>[ApiProperty]</c> (the <see cref="ResponseBody"/> member scope) the
    /// single source of truth for JSON wire names and member inclusion, so that
    /// per-member <c>[JsonProperty]</c> / <c>[JsonIgnore]</c> become redundant.
    ///
    /// <para>The same contract drives both serialization and deserialization,
    /// keeping inbound and outbound wire names symmetric.</para>
    ///
    /// <para><b>Strictly gated.</b> A type is governed only when BOTH:
    /// <list type="number">
    /// <item>its declaring assembly was opted in via
    /// <see cref="RegisterManagedAssembly"/>, AND</item>
    /// <item>it declares at least one member tagged for the
    /// <see cref="ResponseBody"/> scope.</item>
    /// </list>
    /// Every other type retains stock Newtonsoft behavior, so framework types
    /// and not-yet-migrated (V2) resources are never affected.</para>
    /// </summary>
    public sealed class ApiPropertyContractResolver : DefaultContractResolver
    {
        public static ApiPropertyContractResolver Instance { get; } = new ApiPropertyContractResolver();

        private static readonly ConcurrentDictionary<Assembly, bool> managedAssemblies = new();
        private readonly ConcurrentDictionary<Type, bool> governedTypeCache = new();

        private ApiPropertyContractResolver() { }

        /// <summary>
        /// Opt an assembly's V3-managed types into ApiProperty-driven wire
        /// naming. Idempotent; intended to be called once at application start.
        /// </summary>
        public static void RegisterManagedAssembly(Assembly assembly)
        {
            if (assembly is null) throw new ArgumentNullException(nameof(assembly));
            managedAssemblies[assembly] = true;
        }

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);

            var declaringType = member.DeclaringType;
            if (declaringType is null || !IsGoverned(declaringType))
                return property;

            if (TryGetResponseWireName(member, out var wireName))
            {
                property.PropertyName = wireName;
                property.Ignored = false;
            }
            else
            {
                // Governed type, but this member is not tagged for the
                // ResponseBody scope -> it does not appear on the wire. This is
                // what replaces the now-redundant [JsonIgnore].
                property.Ignored = true;
            }

            return property;
        }

        private bool IsGoverned(Type type) =>
            governedTypeCache.GetOrAdd(type, t =>
                managedAssemblies.ContainsKey(t.Assembly) && DeclaresResponseMember(t));

        private static bool DeclaresResponseMember(Type type)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
            foreach (var field in type.GetFields(flags))
                if (HasResponseScope(field))
                    return true;
            foreach (var prop in type.GetProperties(flags))
                if (HasResponseScope(prop))
                    return true;
            return false;
        }

        private static bool HasResponseScope(MemberInfo member) =>
            member.GetCustomAttributes(inherit: true)
                .OfType<IIncludeInMemberScope<ResponseBody>>()
                .Any(includer => includer.Include(member));

        private static bool TryGetResponseWireName(MemberInfo member, out string wireName)
        {
            var includer = member.GetCustomAttributes(inherit: true)
                .OfType<IIncludeInMemberScope<ResponseBody>>()
                .FirstOrDefault(inc => inc.Include(member));
            if (includer is null)
            {
                wireName = null;
                return false;
            }

            var name = includer.GetWireName(member);
            wireName = string.IsNullOrEmpty(name) ? member.Name : name;
            return true;
        }
    }
}
