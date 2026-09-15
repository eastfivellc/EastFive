#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace EastFive.Configuration
{
    /// <summary>One declared member of a configuration type, resolved from its attribute.</summary>
    public sealed record ConfigurationMember(
        PropertyInfo Property,
        string Key,
        ConfigurationMemberKind Kind,
        string? Default,
        string? Purpose)
    {
        public bool IsSecret => Kind == ConfigurationMemberKind.Secret;
    }

    /// <summary>
    /// The declared members of an <see cref="IProvideConfiguration"/> type, reflected once per
    /// type and validated as a DECLARATION (a malformed declaration is a programming error and
    /// throws here; a malformed VALUE is a <see cref="ConfigIssue"/> and never throws). The
    /// static key sets are what <c>IDescribeLibrary.ConfigurationKeys</c> /
    /// <c>PublicConfigurationKeys</c> delegate to, so the typed config is their single source.
    /// </summary>
    public sealed class ConfigurationMembers
    {
        private static readonly ConcurrentDictionary<Type, ConfigurationMembers> cache = new();

        public Type ConfigurationType { get; }
        public IReadOnlyList<ConfigurationMember> Members { get; }

        private ConfigurationMembers(Type configurationType, ConfigurationMember[] members)
        {
            ConfigurationType = configurationType;
            Members = members;
        }

        public static ConfigurationMembers For<TConfig>() where TConfig : IProvideConfiguration
            => For(typeof(TConfig));

        public static ConfigurationMembers For(Type configurationType)
            => cache.GetOrAdd(configurationType, Reflect);

        /// <summary>Every declared key, in declaration order.</summary>
        public static string[] Keys<TConfig>() where TConfig : IProvideConfiguration
            => For<TConfig>().Members.Select(member => member.Key).ToArray();

        /// <summary>Declared keys of members NOT marked <see cref="ConfigurationMemberKind.Secret"/>.</summary>
        public static string[] PublicKeys<TConfig>() where TConfig : IProvideConfiguration
            => For<TConfig>().Members.Where(member => !member.IsSecret).Select(member => member.Key).ToArray();

        /// <summary>Declared keys of members marked <see cref="ConfigurationMemberKind.Secret"/>.</summary>
        public static string[] SecretKeys<TConfig>() where TConfig : IProvideConfiguration
            => For<TConfig>().Members.Where(member => member.IsSecret).Select(member => member.Key).ToArray();

        /// <summary>The environment-global reference for a secret purpose: the declared key.</summary>
        public SecretReference DeclaredSecretReference(string purpose)
            => Members
                .Where(member => member.IsSecret && string.Equals(member.Purpose, purpose, StringComparison.Ordinal))
                .Select(member => new SecretReference(member.Key, purpose))
                .FirstOrDefault()
                ?? throw new InvalidOperationException(
                    $"{ConfigurationType.Name} declares no secret member with purpose '{purpose}'.");

        private static ConfigurationMembers Reflect(Type configurationType)
        {
            if (!typeof(IProvideConfiguration).IsAssignableFrom(configurationType))
                throw new InvalidOperationException($"{configurationType.Name} does not implement {nameof(IProvideConfiguration)}.");

            // The BCL extension, explicitly: EastFive's own GetCustomAttribute<T> THROWS when the
            // attribute is absent, and undeclared public properties (derived values) are legitimate.
            var members = configurationType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => (property, attribute: System.Reflection.CustomAttributeExtensions
                    .GetCustomAttribute<ConfigurationMemberAttribute>(property, inherit: true)))
                .Where(pair => pair.attribute != null)
                .OrderBy(pair => pair.property.MetadataToken) // declaration order; GetProperties() does not promise one
                .Select(pair => Declare(configurationType, pair.property, pair.attribute!))
                .ToArray();

            var duplicateKey = members.GroupBy(member => member.Key, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
            if (duplicateKey != null)
                throw new InvalidOperationException($"{configurationType.Name} declares key [{duplicateKey.Key}] more than once.");

            var duplicatePurpose = members.Where(member => member.IsSecret)
                .GroupBy(member => member.Purpose, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
            if (duplicatePurpose != null)
                throw new InvalidOperationException($"{configurationType.Name} declares secret purpose '{duplicatePurpose.Key}' more than once.");

            return new ConfigurationMembers(configurationType, members);
        }

        private static ConfigurationMember Declare(Type configurationType, PropertyInfo property, ConfigurationMemberAttribute attribute)
        {
            var where = $"{configurationType.Name}.{property.Name} [{attribute.Key}]";
            if (property.SetMethod == null)
                throw new InvalidOperationException($"{where} needs a setter (init is fine) so the loader can populate it.");
            if (!ConfigurationValueConverter.IsSupported(property.PropertyType))
                throw new InvalidOperationException($"{where} is typed {property.PropertyType.Name}, which the loader cannot convert from a string.");

            switch (attribute.Kind)
            {
                case ConfigurationMemberKind.Secret when property.PropertyType != typeof(Secret):
                    throw new InvalidOperationException($"{where} is Secret and must be typed {nameof(Secret)}, not {property.PropertyType.Name}.");
                case ConfigurationMemberKind.Secret when attribute.Purpose is null or "":
                    throw new InvalidOperationException($"{where} is Secret and needs a Purpose.");
                case ConfigurationMemberKind.Secret when attribute.Default != null:
                    throw new InvalidOperationException($"{where} is Secret and cannot be Defaulted; the two are mutually exclusive.");
                case ConfigurationMemberKind.Defaulted when attribute.Default == null:
                    throw new InvalidOperationException($"{where} is Defaulted and needs a Default.");
                case not ConfigurationMemberKind.Defaulted when attribute.Default != null:
                    throw new InvalidOperationException($"{where} carries a Default but is {attribute.Kind}; use Defaulted.");
                case not ConfigurationMemberKind.Secret when attribute.Purpose != null:
                    throw new InvalidOperationException($"{where} carries a Purpose but is {attribute.Kind}; only Secret members have one.");
                case not ConfigurationMemberKind.Secret when property.PropertyType == typeof(Secret):
                    throw new InvalidOperationException($"{where} is typed {nameof(Secret)} but is {attribute.Kind}; mark it Secret.");
            }

            if (attribute.Kind == ConfigurationMemberKind.Optional
                && property.PropertyType.IsValueType
                && Nullable.GetUnderlyingType(property.PropertyType) == null)
                throw new InvalidOperationException($"{where} is Optional and must be nullable so absence is explicit.");

            return new ConfigurationMember(property, attribute.Key, attribute.Kind, attribute.Default, attribute.Purpose);
        }
    }
}
