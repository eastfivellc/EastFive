#nullable enable
using System;
using System.Linq;

namespace EastFive.Configuration
{
    public enum ConfigIssueCategory
    {
        /// <summary>The source answered and the member is not there.</summary>
        Missing,

        /// <summary>A value was obtained (read, defaulted or supplied) and failed conversion or
        /// <see cref="IProvideConfiguration.Validate"/>.</summary>
        Invalid,

        /// <summary>The source could not answer (denied, throttled, timed out, not initialized).
        /// Distinct from <see cref="Missing"/> so a vault outage is never treated as "unset".</summary>
        SourceFailure,

        /// <summary>A synchronous reader asked for a configuration the host never loaded at
        /// startup (<see cref="StartupConfiguration"/>): a host wiring bug, not a setting.</summary>
        NotLoaded,
    }

    /// <summary>
    /// What went wrong loading one member: the configuration type, the key, the member's kind, the
    /// category and, when a member's parser refused its value, the <see cref="Problem"/> it named.
    /// A <see cref="ConfigIssueCategory.SourceFailure"/> from the secret store also carries the
    /// store's structured <see cref="Failure"/> (its message becomes the <see cref="Problem"/>).
    /// Carries NO values and NO raw SDK messages, so it is safe to log, return in a response reason,
    /// or surface to a doctor. <see cref="ToString"/> is ASCII-only for the same reason (an
    /// <c>x-reason</c> header 500s on non-ASCII).
    /// </summary>
    public sealed record ConfigIssue(
        Type ConfigurationType,
        string Key,
        ConfigurationMemberKind Kind,
        ConfigIssueCategory Category,
        string? Problem = null,
        SecretFailure? Failure = null)
    {
        public override string ToString()
            => $"{ConfigurationType.Name}[{Key}] ({Kind}) {Category}" + (Problem == null ? string.Empty : $": {Ascii(Problem)}");

        private static string Ascii(string text)
            => new(text.Select(c => c is >= ' ' and <= '~' ? c : '?').ToArray());
    }
}
