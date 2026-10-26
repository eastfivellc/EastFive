#nullable enable
namespace EastFive.Configuration
{
    /// <summary>
    /// A KEYED configuration type: one instance per name (a Ping redirect per tag, SAML per tag),
    /// constructed with that name. Member keys carry the <see cref="Placeholder"/>, which the loader
    /// replaces with <see cref="InstanceKey"/>; the catalog enumerates instances from the configured
    /// keys that match (<see cref="ConfigurationCatalog.InstanceKeys(System.Type, System.Collections.Generic.IEnumerable{string})"/>).
    /// </summary>
    public interface IProvideConfigurationInstanceKey
    {
        public const string Placeholder = "{key}";

        string InstanceKey { get; }
    }
}
