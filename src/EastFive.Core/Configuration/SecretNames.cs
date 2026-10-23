#nullable enable
using System;
using System.Text.RegularExpressions;

namespace EastFive.Configuration
{
    /// <summary>
    /// The secret naming scheme. Names are never hand-written: a configuration type's
    /// <see cref="ConfigurationTypeAttribute"/> supplies <c>Type</c>, its account member's VALUE
    /// supplies <c>Account</c>, and the secret member's property name supplies <c>Property</c>.
    /// Every name is a valid Azure Key Vault secret name (<c>[A-Za-z0-9-]</c>, at most 127
    /// characters), so it is read directly by a secret client with no key mapping.
    /// </summary>
    public static class SecretNames
    {
        public const int MaximumLength = 127;

        private static readonly Regex NameShape = new("^[A-Za-z0-9-]+$", RegexOptions.CultureInvariant);
        private static readonly Regex AccountShape = new("^[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?$", RegexOptions.CultureInvariant);

        public static bool IsValidName(string? name)
            => name is { Length: > 0 and <= MaximumLength } && NameShape.IsMatch(name);

        /// <summary>An account id that can be one segment of a secret name: letters, digits and
        /// inner dashes. Anything else is refused rather than mapped, so two accounts can never
        /// collide on one generated name.</summary>
        public static bool IsValidAccount(string? account)
            => account is { Length: > 0 } && AccountShape.IsMatch(account);

        /// <summary><c>{Type}-{Account}-{Property}</c>, or <c>{Type}-{Property}</c> without an account.</summary>
        public static string Generate(string type, string? account, string property)
        {
            if (account != null && !IsValidAccount(account))
                throw new ArgumentException("An account id in a secret name is letters, digits and inner dashes only.", nameof(account));
            var name = account == null ? $"{type}-{property}" : $"{type}-{account}-{property}";
            return IsValidName(name)
                ? name
                : throw new ArgumentException($"Generated secret name for {type}.{property} is not a valid vault secret name.");
        }
    }
}
