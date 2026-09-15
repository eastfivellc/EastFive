#nullable enable
using System;

namespace EastFive.Configuration
{
    /// <summary>How a configuration member's absence is treated by the loader.</summary>
    public enum ConfigurationMemberKind
    {
        /// <summary>Absent is a configuration problem: the caller's <c>onMissing</c> decides.</summary>
        Required,

        /// <summary>Absent is a legitimate partial mode: the member is <c>null</c> in the typed
        /// config and the OPERATION that needs it reports not-configured.</summary>
        Optional,

        /// <summary>Absent applies <see cref="ConfigurationMemberAttribute.Default"/>, which is
        /// converted and validated like a read value.</summary>
        Defaulted,

        /// <summary>Secret material, read via <see cref="IProvideConfiguration.SecretReference"/>
        /// and typed as <see cref="Secret"/>; confirmed-absent is <c>onMissing</c>, unreachable is
        /// <c>onSourceFailure</c>. Never defaulted, never substituted with an empty credential.</summary>
        Secret,
    }

    /// <summary>
    /// Declares one property of an <see cref="IProvideConfiguration"/> type as a configuration
    /// member: the config key it is read from, its <see cref="Kind"/>, the string-form
    /// <see cref="Default"/> for <see cref="ConfigurationMemberKind.Defaulted"/> members, and the
    /// <see cref="Purpose"/> a <see cref="ConfigurationMemberKind.Secret"/> member is resolved by.
    /// Members are processed in declaration order.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class ConfigurationMemberAttribute : Attribute
    {
        public ConfigurationMemberAttribute(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("A configuration member needs a key.", nameof(key));
            Key = key;
        }

        /// <summary>The config key (e.g. <c>Email.SenderAddress</c>) the member is read from;
        /// for secrets, the environment-global key the declared purpose resolves to by default.</summary>
        public string Key { get; }

        public ConfigurationMemberKind Kind { get; init; } = ConfigurationMemberKind.Required;

        /// <summary>String form of the default (all config values originate as strings). Only
        /// meaningful with <see cref="ConfigurationMemberKind.Defaulted"/>.</summary>
        public string? Default { get; init; }

        /// <summary>What the secret is for, passed to <see cref="IProvideConfiguration.SecretReference"/>.
        /// Only meaningful with <see cref="ConfigurationMemberKind.Secret"/>.</summary>
        public string? Purpose { get; init; }
    }
}
