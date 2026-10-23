#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace EastFive.Configuration
{
    /// <summary>One member as an editor or doctor renders it.</summary>
    public sealed record ConfigurationMemberDescription(
        string Property,
        string? Key,
        ConfigurationMemberKind Kind,
        string? Default,
        bool IsAccount,
        ConfigurationMemberMetadata Metadata,
        string? SecretNameTemplate,
        bool HasLookup,
        string? AccountSegmentTranslation = null);

    /// <summary>One configuration type: its generated-name segment, metadata, members, and whether it
    /// can test a draft.</summary>
    public sealed record ConfigurationTypeDescription(
        Type Type,
        string? TypeName,
        string Label,
        string? Description,
        string? AccountKey,
        bool GeneratesSecretNames,
        bool HasTest,
        IReadOnlyList<ConfigurationMemberDescription> Members)
    {
        /// <summary>One instance per name (<see cref="IProvideConfigurationInstanceKey"/>); member keys
        /// carry the placeholder and <see cref="ConfigurationCatalog.InstanceKeys(Type, IEnumerable{string})"/>
        /// lists the instances.</summary>
        public bool IsKeyed => typeof(IProvideConfigurationInstanceKey).IsAssignableFrom(Type);

        /// <summary>The secret names this type reads for an account (or globally, when it has no
        /// account member). Only meaningful when <see cref="GeneratesSecretNames"/>.</summary>
        public IEnumerable<string> SecretNames(string? account = null)
            => Members.Where(member => member.SecretNameTemplate != null)
                .Select(member => member.SecretNameTemplate!.Replace(ConfigurationCatalog.AccountPlaceholder,
                    (account == null ? null : ConfigurationMembers.For(Type).AccountSegmentOf(account.Trim())) ?? ConfigurationCatalog.AccountPlaceholder));

        /// <summary>
        /// The account rule for a DRAFT value, before it is written: null when <paramref name="account"/>
        /// (translated as this type declares) can be the account segment of a secret name, otherwise the
        /// reason, naming the account member's key. Null for a type that names no account.
        /// </summary>
        public string? AccountProblem(string? account)
            => ConfigurationMembers.For(Type).AccountProblem(account);

        public ITestConfiguration? Test
            => Type.GetCustomAttributes(inherit: true).OfType<ITestConfiguration>().FirstOrDefault();
    }

    /// <summary>
    /// Discovery: every concrete configuration type in a set of assemblies with its members, kinds,
    /// metadata and generated secret names — what an editor and a doctor render from.
    /// </summary>
    public static class ConfigurationCatalog
    {
        public const string AccountPlaceholder = "{account}";

        /// <summary>Every concrete configuration type in <paramref name="assemblies"/>. A malformed
        /// declaration throws (a programming error), exactly as loading it would.</summary>
        public static ConfigurationTypeDescription[] For(IEnumerable<Assembly> assemblies)
            => For(assemblies.Distinct().SelectMany(LoadableTypes));

        /// <summary><see cref="For(IEnumerable{Assembly})"/> without the types whose declaration is
        /// malformed — for a renderer that must show everything else rather than fail.</summary>
        public static ConfigurationTypeDescription[] WellFormed(IEnumerable<Assembly> assemblies)
            => assemblies.Distinct().SelectMany(LoadableTypes)
                .Where(type => type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false }
                    && typeof(IProvideConfiguration).IsAssignableFrom(type)
                    && DeclaredMembersOrNull(type) != null)
                .Distinct()
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .Select(Describe)
                .ToArray();

        public static ConfigurationTypeDescription[] For(IEnumerable<Type> types)
            => types
                .Where(type => type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false }
                    && typeof(IProvideConfiguration).IsAssignableFrom(type))
                .Distinct()
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .Select(Describe)
                .ToArray();

        public static ConfigurationTypeDescription Describe(Type configurationType)
        {
            var declared = ConfigurationMembers.For(configurationType);
            var attribute = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<ConfigurationTypeAttribute>(configurationType, inherit: true);
            var generates = declared.TypeName != null && !OverridesSecretReference(configurationType);
            var members = declared.Members
                .Select(member => new ConfigurationMemberDescription(
                    member.Property.Name,
                    member.Key,
                    member.Kind,
                    member.Default,
                    member.IsAccount,
                    member.Metadata!,
                    member.IsSecret && generates
                        ? declared.AccountMember == null
                            ? $"{declared.TypeName}-{member.Property.Name}"
                            : $"{declared.TypeName}-{AccountPlaceholder}-{member.Property.Name}"
                        : null,
                    member.Lookup != null,
                    member.IsAccount ? member.AccountSegment?.GetType().Name : null))
                .ToArray();
            return new ConfigurationTypeDescription(
                configurationType,
                declared.TypeName,
                attribute?.Label ?? declared.TypeName ?? configurationType.Name,
                attribute?.Description,
                declared.AccountMember?.Key,
                generates,
                configurationType.GetCustomAttributes(inherit: true).OfType<ITestConfiguration>().Any(),
                members);
        }

        /// <summary>
        /// The instances of a keyed type that <paramref name="configuredKeys"/> name: every value of the
        /// placeholder that completes one of its members' keys (or fallback keys), distinct and sorted.
        /// </summary>
        public static string[] InstanceKeys(Type keyedType, IEnumerable<string> configuredKeys)
        {
            var templates = ConfigurationMembers.For(keyedType).Members
                .SelectMany(member => new[] { member.Key, member.FallbackKey })
                .OfType<string>()
                .Where(key => key.Contains(IProvideConfigurationInstanceKey.Placeholder, StringComparison.Ordinal))
                .Select(key => new System.Text.RegularExpressions.Regex(
                    "^" + System.Text.RegularExpressions.Regex.Escape(key).Replace(
                        System.Text.RegularExpressions.Regex.Escape(IProvideConfigurationInstanceKey.Placeholder), "(?<key>[^.:]+)") + "$",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                .ToArray();
            return configuredKeys
                .SelectMany(key => templates.Select(template => template.Match(key)).Where(match => match.Success))
                .Select(match => match.Groups["key"].Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        /// <summary>The instances of a keyed type the ambient configuration names.</summary>
        public static string[] InstanceKeys(Type keyedType)
            => InstanceKeys(keyedType, EastFive.Web.Configuration.ConfigurationExtensions.ConfiguredKeys());

        private static readonly Lazy<HashSet<string>> declaredValueKeys = new(() =>
            new HashSet<string>(
                AppDomain.CurrentDomain.GetAssemblies()
                    .Where(assembly => !assembly.IsDynamic)
                    .SelectMany(LoadableTypes)
                    .Where(type => type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false }
                        && typeof(IProvideConfiguration).IsAssignableFrom(type))
                    .SelectMany(DeclaredMembersOrNone)
                    .Where(member => !member.IsSecret)
                    .SelectMany(member => new[] { member.Key, member.FallbackKey })
                    .OfType<string>()
                    .Where(key => !key.Contains(IProvideConfigurationInstanceKey.Placeholder, StringComparison.Ordinal)),
                StringComparer.OrdinalIgnoreCase),
            System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);

        private static IEnumerable<ConfigurationMember>? DeclaredMembersOrNull(Type type)
        {
            try
            {
                return ConfigurationMembers.For(type).Members;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>A malformed declaration throws when its type is loaded; it declares nothing here.</summary>
        private static IEnumerable<ConfigurationMember> DeclaredMembersOrNone(Type type)
            => DeclaredMembersOrNull(type) ?? Enumerable.Empty<ConfigurationMember>();

        /// <summary>Whether a non-secret member of any loaded configuration type declares <paramref name="key"/>.
        /// Assemblies loaded after the first call are not seen; framework and host assemblies load first.</summary>
        public static bool IsDeclaredValueKey(string key) => declaredValueKeys.Value.Contains(key);

        /// <summary>The current value of a VALUE member, read by its declared key — what a catalog
        /// renderer shows. Null for secrets (never read here) and keyed templates.</summary>
        public static string? CurrentValue(ConfigurationMemberDescription member)
            => member.Kind == ConfigurationMemberKind.Secret || member.Key == null
                    || member.Key.Contains(IProvideConfigurationInstanceKey.Placeholder, StringComparison.Ordinal)
                ? null
                : ConfigurationLoader.ReadAmbient(member.Key);

        /// <summary>The current value of a VALUE member of a keyed type for one instance, read by the
        /// instance's key and then its fallback key. Null for secrets.</summary>
        public static string? CurrentValue(Type keyedType, ConfigurationMemberDescription member, string instanceKey)
        {
            if (member.Kind == ConfigurationMemberKind.Secret || member.Key == null)
                return null;
            var declared = ConfigurationMembers.For(keyedType).Members.First(candidate => candidate.Property.Name == member.Property);
            return new[] { declared.Key, declared.FallbackKey }
                .OfType<string>()
                .Select(key => ConfigurationLoader.ReadAmbient(
                    key.Replace(IProvideConfigurationInstanceKey.Placeholder, instanceKey, StringComparison.Ordinal)))
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        }

        /// <summary>A type that names its own secrets instead of using the generated scheme.</summary>
        public static bool OverridesSecretReference(Type configurationType)
            => configurationType.GetInterfaceMap(typeof(IProvideConfiguration)).InterfaceMethods
                .Zip(configurationType.GetInterfaceMap(typeof(IProvideConfiguration)).TargetMethods)
                .Any(pair => pair.First.Name == nameof(IProvideConfiguration.SecretReference)
                    && pair.Second.DeclaringType != typeof(IProvideConfiguration));

        private static IEnumerable<Type> LoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException partial)
            {
                return partial.Types.OfType<Type>();
            }
        }
    }
}
