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
    }
}
