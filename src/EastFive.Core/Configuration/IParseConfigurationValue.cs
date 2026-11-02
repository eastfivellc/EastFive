#nullable enable
using System;
using System.Collections.Generic;

namespace EastFive.Configuration
{
    /// <summary>
    /// Implemented by an attribute on a configuration MEMBER whose type the loader does not convert
    /// itself (a key, a shared secret): the attribute carries the member's constraints and turns the
    /// text into a <see cref="ValueType"/>. The loader, inspector, catalog and generator runner all
    /// bind through it, so a value that fails to parse is the member's load error with
    /// <see cref="ConfigIssue.Problem"/> naming why. A member declares at most one.
    /// </summary>
    /// <remarks>
    /// A parse message names the KIND of value expected and found (e.g. "expected EC P-384 private
    /// key; got P-256"), never the value: it is logged, shown and returned in an ASCII header.
    /// </remarks>
    public interface IParseConfigurationValue
    {
        /// <summary>The type parsed values have; the member's type must be able to hold it.</summary>
        Type ValueType { get; }

        /// <summary>What the member holds, for an editor and a doctor (e.g. "EC P-384 private key").</summary>
        string ValueKind { get; }

        /// <summary>The encodings the parser accepts, for an editor's hint.</summary>
        IReadOnlyList<string> AcceptedFormats { get; }

        /// <summary>The value is key material: the member must be a <see cref="ConfigurationMemberKind.Secret"/>,
        /// read from the secret store, never the values store.</summary>
        bool IsSecretMaterial { get; }

        TResult Parse<TResult>(string text, Func<object, TResult> onParsed, Func<string, TResult> onInvalid);
    }
}
