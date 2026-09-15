#nullable enable
using System;

namespace EastFive.Configuration
{
    /// <summary>
    /// One driver's typed configuration: a class whose properties are declared with
    /// <see cref="ConfigurationMemberAttribute"/> and populated by <see cref="ConfigurationLoader"/>.
    /// The implementing type owns which members exist, their kind (<see cref="ConfigurationMemberKind"/>),
    /// account identity (constructor inputs, never config members), secret-reference derivation
    /// (<see cref="SecretReference(string)"/>), semantic validation (<see cref="Validate"/>), defaults
    /// and partial modes. By CONVENTION a concrete type also exposes <c>LoadDriver()</c>, which
    /// constructs the driver it configures so callers never learn the driver's constructor
    /// arguments; that is deliberately NOT an interface member, because the loader never sees
    /// drivers and a configuration type is legitimate without one.
    /// </summary>
    /// <remarks>
    /// Prototyped in a consumer against three drivers (an email client and two runtime
    /// adapters) before promotion here; the shape is the one that survived that exercise.
    /// </remarks>
    public interface IProvideConfiguration
    {
        /// <summary>
        /// Which secret serves the member declared with this <c>Purpose</c>. Today a config key
        /// that the whole-vault <c>IConfiguration</c> provider already serves; later a vault
        /// alias + name + version selected by account identity. The default answers with the
        /// member's declared key, which is the environment-global one.
        /// </summary>
        SecretReference SecretReference(string purpose)
            => ConfigurationMembers.For(GetType()).DeclaredSecretReference(purpose);

        /// <summary>
        /// Semantic validation once every member has been converted (cross-member rules, shapes
        /// conversion cannot check). Returns the config KEY of the first member that fails, or
        /// <c>null</c> when the configuration is coherent. Never returns a value.
        /// </summary>
        string? Validate() => null;
    }
}
