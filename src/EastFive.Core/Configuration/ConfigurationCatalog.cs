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
