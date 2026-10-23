#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EastFive.Configuration
{
    /// <summary>
    /// A configuration as an editor holds it before writing it: values by key and secrets by name.
    /// Hooks read a draft; they never read the ambient configuration.
    /// </summary>
    public sealed record ConfigurationDraft(Func<string, string?> ReadValue, ISecretStore Secrets)
    {
        public static ConfigurationDraft From(IReadOnlyDictionary<string, string> values, IReadOnlyDictionary<string, string> secrets)
            => new(key => values.TryGetValue(key, out var value) ? value : null, new InMemorySecretStore(secrets, "draft"));
    }

    /// <summary>One choice a lookup offers: the value written and the label shown.</summary>
    public sealed record ConfigurationChoice(string Value, string Label);

    /// <summary>
    /// Implemented by an attribute on a configuration MEMBER: the choices an editor offers for it,
    /// computed from the draft so far (e.g. the phone numbers an API key can see). Never returns
    /// secret material as a choice.
    /// </summary>
    public interface IProvideConfigurationLookup
    {
        Task<TResult> LookupAsync<TResult>(ConfigurationMember member, ConfigurationDraft draft,
            Func<IReadOnlyList<ConfigurationChoice>, TResult> onChoices,
            Func<string, TResult> onUnavailable);
    }

    /// <summary>
    /// Implemented by an attribute on a configuration TYPE: a live check of a draft (does the key
    /// authenticate, does the endpoint answer). The reason strings carry no secret material.
    /// </summary>
    public interface ITestConfiguration
    {
        Task<TResult> TestAsync<TResult>(Type configurationType, ConfigurationDraft draft,
            Func<string, TResult> onPassed,
            Func<string, TResult> onFailed);
    }
}
