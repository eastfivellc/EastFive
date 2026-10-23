#nullable enable
using System;
using System.Text.RegularExpressions;

namespace EastFive.Configuration
{
    /// <summary>
    /// Declares the <c>Type</c> segment of every secret name an <see cref="IProvideConfiguration"/>
    /// type generates: <c>{Type}-{Account}-{Property}</c> when the type declares an account member
    /// (<see cref="ConfigurationMemberAttribute.Account"/>), <c>{Type}-{Property}</c> otherwise.
    /// Inherited, so an abstract base (one storage account, many stores) names every concrete
    /// type the same and stores that share an account share one secret.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class ConfigurationTypeAttribute : Attribute
    {
        private static readonly Regex NameShape = new("^[A-Za-z0-9]+$", RegexOptions.CultureInvariant);

        public ConfigurationTypeAttribute(string name)
        {
            if (name is null || !NameShape.IsMatch(name))
                throw new ArgumentException("A configuration type name is letters and digits only; it is one segment of a vault secret name.", nameof(name));
            Name = name;
        }

        /// <summary>The <c>Type</c> segment of generated secret names (e.g. <c>AzureStorage</c>).</summary>
        public string Name { get; }

        /// <summary>Human label for an editor (defaults to <see cref="Name"/>).</summary>
        public string? Label { get; init; }

        /// <summary>What this configuration configures.</summary>
        public string? Description { get; init; }

        private ConfigurationScope? scope;

        /// <summary>The scope of every member that does not declare its own.</summary>
        public ConfigurationScope Scope { get => scope ?? ConfigurationScope.Environment; init => scope = value; }

        /// <summary>Whether <see cref="Scope"/> was set on this type.</summary>
        public bool ScopeDeclared => scope.HasValue;
    }
}
