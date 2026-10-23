#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace EastFive.Configuration
{
    /// <summary>Editor metadata for one member (no behaviour; see <see cref="IProvideConfigurationLookup"/>
    /// and <see cref="ITestConfiguration"/> for the hooks).</summary>
    public sealed record ConfigurationMemberMetadata(
        string Label,
        string? Description,
        string? Location,
        string? MoreInfo,
        ConfigurationScope Scope,
        ConfigurationFieldKind FieldKind);

    /// <summary>One declared member of a configuration type, resolved from its attribute.</summary>
    public sealed record ConfigurationMember(
        PropertyInfo Property,
        string? Key,
        ConfigurationMemberKind Kind,
        string? Default,
        string? Purpose,
        bool IsAccount = false,
        ConfigurationMemberMetadata? Metadata = null)
    {
        public bool IsSecret => Kind == ConfigurationMemberKind.Secret;

        /// <summary>The account member's translation to a secret-name segment, if it declares one.</summary>
        public IProvideSecretAccountSegment? AccountSegment
            => Property.GetCustomAttributes(inherit: true).OfType<IProvideSecretAccountSegment>().FirstOrDefault();

        /// <summary>The member's lookup hook, when an attribute on the property provides one.</summary>
        public IProvideConfigurationLookup? Lookup
            => Property.GetCustomAttributes(inherit: true).OfType<IProvideConfigurationLookup>().FirstOrDefault();
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
        private static readonly Regex PropertyShape = new("^[A-Za-z0-9]+$", RegexOptions.CultureInvariant);

        public Type ConfigurationType { get; }
        public IReadOnlyList<ConfigurationMember> Members { get; }

        /// <summary>The <see cref="ConfigurationTypeAttribute"/> name; <c>null</c> when undeclared
        /// (legal only for a type whose secrets are all named by an overridden
        /// <see cref="IProvideConfiguration.SecretReference"/>, or that has none).</summary>
        public string? TypeName { get; }

        /// <summary>The member whose value scopes generated secret names, if any.</summary>
        public ConfigurationMember? AccountMember { get; }

        private ConfigurationMembers(Type configurationType, ConfigurationMember[] members, string? typeName)
        {
            ConfigurationType = configurationType;
            Members = members;
            TypeName = typeName;
            AccountMember = members.SingleOrDefault(member => member.IsAccount);
        }

        public static ConfigurationMembers For<TConfig>() where TConfig : IProvideConfiguration
            => For(typeof(TConfig));

        public static ConfigurationMembers For(Type configurationType)
            => cache.GetOrAdd(configurationType, Reflect);

        public bool HasSecrets => Members.Any(member => member.IsSecret);

        /// <summary>Every declared values key, in declaration order (secrets have none).</summary>
        public static string[] Keys<TConfig>() where TConfig : IProvideConfiguration
            => For<TConfig>().Members.Select(member => member.Key).OfType<string>().ToArray();

        /// <summary>Declared values keys; every one is public, since secrets have names, not keys.</summary>
        public static string[] PublicKeys<TConfig>() where TConfig : IProvideConfiguration
            => Keys<TConfig>();

        /// <summary>The secret names an instance reads (account member already set, if any).</summary>
        public string[] SecretNamesOf(IProvideConfiguration configuration)
            => Members.Where(member => member.IsSecret)
                .Select(member => configuration.SecretReference(member.Purpose!).Name)
                .ToArray();

        /// <summary>The secret member declared for <paramref name="purpose"/> (its
        /// <see cref="ConfigurationMemberAttribute.Purpose"/>, or its property name).</summary>
        public ConfigurationMember SecretMember(string purpose)
            => Members.FirstOrDefault(member => member.IsSecret && string.Equals(member.Purpose, purpose, StringComparison.Ordinal))
                ?? throw new InvalidOperationException(
                    $"{ConfigurationType.Name} declares no secret member with purpose '{purpose}'.");

        /// <summary>The account segment an instance scopes its secret names by: the trimmed value of the
        /// account member, translated by its <see cref="IProvideSecretAccountSegment"/> when it declares
        /// one; <c>null</c> when the type declares no account member.</summary>
        public string? AccountOf(object configuration)
            => AccountMember == null ? null : AccountSegmentOf((AccountMember.Property.GetValue(configuration) as string)?.Trim());

        /// <summary>An account VALUE as the secret-name segment this type would use (unvalidated).</summary>
        public string? AccountSegmentOf(string? account)
            => account == null || AccountMember?.AccountSegment is not { } translation
                ? account
                : translation.SecretAccountSegment(account);

        /// <summary>Null when <paramref name="account"/>, translated as the account member declares, is a
        /// valid secret-name segment (or the type has no account member); otherwise the reason, naming
        /// the member's key. What load-time validation and an editor's draft check both apply.</summary>
        public string? AccountProblem(string? account)
        {
            if (AccountMember == null)
                return null;
            var segment = AccountSegmentOf(account?.Trim());
            return SecretNames.IsValidAccount(segment)
                ? null
                : $"[{AccountMember.Key}] '{account}' cannot name a secret"
                    + (AccountMember.AccountSegment is { } translation ? $" (translated by {translation.GetType().Name} to '{segment}')" : string.Empty)
                    + ": an account segment is letters, digits and inner dashes only.";
        }

        /// <summary>The account member's value cannot be one segment of a secret name.</summary>
        public bool HasInvalidAccount(object configuration)
            => AccountMember != null && !SecretNames.IsValidAccount(AccountOf(configuration));

        /// <summary>
        /// The GENERATED reference for a secret purpose: <c>{Type}-{Account}-{Property}</c>, or
        /// <c>{Type}-{Property}</c> for a type without an account member. Throws when the type
        /// declares no <see cref="ConfigurationTypeAttribute"/> or the account value cannot be a
        /// name segment; the loader checks the account first and reports it as invalid.
        /// </summary>
        public SecretReference GeneratedSecretReference(object configuration, string purpose)
        {
            var member = SecretMember(purpose);
            if (TypeName == null)
                throw new InvalidOperationException(
                    $"{ConfigurationType.Name} declares secret members without [{nameof(ConfigurationTypeAttribute)}]; secret names are generated from it.");
            if (HasInvalidAccount(configuration))
                throw new InvalidOperationException(
                    $"{ConfigurationType.Name}'s account [{AccountMember!.Key}] cannot name a secret (letters, digits and inner dashes only).");
            return new SecretReference(SecretNames.Generate(TypeName, AccountOf(configuration), member.Property.Name), purpose);
        }

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

            var duplicateKey = members.Where(member => member.Key != null)
                .GroupBy(member => member.Key, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
            if (duplicateKey != null)
                throw new InvalidOperationException($"{configurationType.Name} declares key [{duplicateKey.Key}] more than once.");

            var duplicatePurpose = members.Where(member => member.IsSecret)
                .GroupBy(member => member.Purpose, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
            if (duplicatePurpose != null)
                throw new InvalidOperationException($"{configurationType.Name} declares secret purpose '{duplicatePurpose.Key}' more than once.");

            var misplaced = members.FirstOrDefault(member => !member.IsAccount
                && member.Property.GetCustomAttributes(inherit: true).OfType<IProvideSecretAccountSegment>().Any());
            if (misplaced != null)
                throw new InvalidOperationException(
                    $"{configurationType.Name}.{misplaced.Property.Name} declares a secret account segment but is not the account member.");
            var doubled = members.FirstOrDefault(member => member.Property.GetCustomAttributes(inherit: true).OfType<IProvideSecretAccountSegment>().Count() > 1);
            if (doubled != null)
                throw new InvalidOperationException(
                    $"{configurationType.Name}.{doubled.Property.Name} declares more than one secret account segment.");

            var accounts = members.Where(member => member.IsAccount).ToArray();
            if (accounts.Length > 1)
                throw new InvalidOperationException($"{configurationType.Name} declares more than one account member.");
            if (accounts.Length == 1)
            {
                var account = accounts[0];
                if (account.Default != null)
                {
                    var segment = account.AccountSegment?.SecretAccountSegment(account.Default.Trim()) ?? account.Default.Trim();
                    if (!SecretNames.IsValidAccount(segment))
                        throw new InvalidOperationException(
                            $"{configurationType.Name}.{account.Property.Name} defaults to an account that cannot name a secret ('{account.Default}').");
                }
                var firstSecret = Array.FindIndex(members, member => member.IsSecret);
                if (firstSecret >= 0 && Array.IndexOf(members, account) > firstSecret)
                    throw new InvalidOperationException(
                        $"{configurationType.Name}.{account.Property.Name} is the account member and must be declared before every secret member.");
            }

            var typeName = System.Reflection.CustomAttributeExtensions
                .GetCustomAttribute<ConfigurationTypeAttribute>(configurationType, inherit: true)?.Name;
            return new ConfigurationMembers(configurationType, members, typeName);
        }

        private static ConfigurationMember Declare(Type configurationType, PropertyInfo property, ConfigurationMemberAttribute attribute)
        {
            var where = $"{configurationType.Name}.{property.Name} [{attribute.Key ?? "(secret)"}]";
            if (property.SetMethod == null)
                throw new InvalidOperationException($"{where} needs a setter (init is fine) so the loader can populate it.");
            if (!ConfigurationValueConverter.IsSupported(property.PropertyType))
                throw new InvalidOperationException($"{where} is typed {property.PropertyType.Name}, which the loader cannot convert from a string.");

            switch (attribute.Kind)
            {
                case ConfigurationMemberKind.Secret when property.PropertyType != typeof(Secret):
                    throw new InvalidOperationException($"{where} is Secret and must be typed {nameof(Secret)}, not {property.PropertyType.Name}.");
                case ConfigurationMemberKind.Secret when attribute.Default != null:
                    throw new InvalidOperationException($"{where} is Secret and cannot be Defaulted; the two are mutually exclusive.");
                case ConfigurationMemberKind.Secret when attribute.Key != null:
                    throw new InvalidOperationException($"{where} is Secret and declares a values key; a secret is read from the secret store by its generated name, never from the values store.");
                case ConfigurationMemberKind.Secret when attribute.Account:
                    throw new InvalidOperationException($"{where} is Secret and cannot be the account member; an account id is a value.");
                case ConfigurationMemberKind.Secret when !PropertyShape.IsMatch(property.Name):
                    throw new InvalidOperationException($"{where} is Secret and its property name is a secret-name segment: letters and digits only.");
                case not ConfigurationMemberKind.Secret when attribute.Key == null:
                    throw new InvalidOperationException($"{where} is {attribute.Kind} and needs a values key; only Secret members are keyless.");
                case ConfigurationMemberKind.Defaulted when attribute.Default == null:
                    throw new InvalidOperationException($"{where} is Defaulted and needs a Default.");
                case not ConfigurationMemberKind.Defaulted when attribute.Default != null:
                    throw new InvalidOperationException($"{where} carries a Default but is {attribute.Kind}; use Defaulted.");
                case not ConfigurationMemberKind.Secret when attribute.Purpose != null:
                    throw new InvalidOperationException($"{where} carries a Purpose but is {attribute.Kind}; only Secret members have one.");
                case not ConfigurationMemberKind.Secret when property.PropertyType == typeof(Secret):
                    throw new InvalidOperationException($"{where} is typed {nameof(Secret)} but is {attribute.Kind}; mark it Secret.");
            }

            if (attribute.Account
                && (property.PropertyType != typeof(string) || attribute.Kind is not (ConfigurationMemberKind.Required or ConfigurationMemberKind.Defaulted)))
                throw new InvalidOperationException($"{where} is the account member: a Required or Defaulted string.");

            if (attribute.Kind == ConfigurationMemberKind.Optional
                && property.PropertyType.IsValueType
                && Nullable.GetUnderlyingType(property.PropertyType) == null)
                throw new InvalidOperationException($"{where} is Optional and must be nullable so absence is explicit.");

            var purpose = attribute.Kind == ConfigurationMemberKind.Secret ? attribute.Purpose ?? property.Name : null;
            var metadata = new ConfigurationMemberMetadata(
                attribute.Label ?? property.Name,
                attribute.Description,
                attribute.Location,
                attribute.MoreInfo,
                attribute.Scope,
                attribute.FieldKind != ConfigurationFieldKind.Auto ? attribute.FieldKind : FieldKindOf(property.PropertyType, attribute));
            return new ConfigurationMember(property, attribute.Key, attribute.Kind, attribute.Default, purpose, attribute.Account, metadata);
        }

        private static ConfigurationFieldKind FieldKindOf(Type propertyType, ConfigurationMemberAttribute attribute)
        {
            var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            return attribute.Kind == ConfigurationMemberKind.Secret ? ConfigurationFieldKind.Secret
                : attribute.Account ? ConfigurationFieldKind.Account
                : type == typeof(Uri) ? ConfigurationFieldKind.Url
                : type == typeof(bool) ? ConfigurationFieldKind.Boolean
                : type == typeof(TimeSpan) ? ConfigurationFieldKind.Duration
                : type.IsEnum ? ConfigurationFieldKind.Enum
                : type == typeof(int) || type == typeof(long) || type == typeof(double) || type == typeof(decimal) ? ConfigurationFieldKind.Number
                : ConfigurationFieldKind.Text;
        }
    }
}
