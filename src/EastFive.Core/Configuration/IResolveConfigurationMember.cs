#nullable enable
using System;

namespace EastFive.Configuration
{
    /// <summary>
    /// Lets a configuration type answer one of its OWN members before any source is consulted —
    /// a value that is a function of members already populated, not a setting. The canonical case
    /// is a storage account named <c>devstoreaccount1</c>: its credential is the emulator's
    /// published well-known string, and no vault entry may redirect it, so the secret store is
    /// never asked. Implemented by the configuration type itself (attribute-interface pattern
    /// applied to the type), consulted for every member in declaration order.
    /// </summary>
    public interface IResolveConfigurationMember
    {
        /// <param name="member">The member about to be read.</param>
        /// <param name="readValue">The VALUES source the load is using (never secrets).</param>
        /// <param name="onResolved">The member's text and a description of where it came from
        /// (no value), converted and validated like a read value.</param>
        /// <param name="onNotResolved">Read the member from its sources as usual.</param>
        /// <param name="onInvalid">The member cannot be resolved: an <see cref="ConfigIssueCategory.Invalid"/>
        /// or <see cref="ConfigIssueCategory.SourceFailure"/> issue naming the key at fault.</param>
        TResult ResolveMember<TResult>(ConfigurationMember member, Func<string, string?> readValue,
            Func<string, string, TResult> onResolved,
            Func<TResult> onNotResolved,
            Func<ConfigIssue, TResult> onInvalid);
    }
}
