using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace EastFive.Api
{
    /// <summary>
    /// Per-method routing description produced by an
    /// <see cref="IMatchRoute"/> attribute. Pure value: a compiled path
    /// regex, the set of HTTP verbs the method accepts, and optional named
    /// query keys that must be present for the route to match.
    ///
    /// The regex is intentionally not exposed; callers ask
    /// <see cref="TryMatch"/> two questions in one call — does the path
    /// match, and what did its named groups capture? Splitting the API
    /// surface this way keeps "did it route?" separate from "what variables
    /// did the route extract?" without compiling two regexes.
    /// </summary>
    public sealed class RouteTemplate
    {
        private readonly Regex pattern;

        public RouteTemplate(string[] verbs, Regex pattern)
        {
            this.Verbs = verbs ?? Array.Empty<string>();
            this.pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
        }

        /// <summary>HTTP verbs (case-insensitive) accepted at this route.</summary>
        private string[] Verbs { get; }

        /// <summary>
        /// Try to match <paramref name="requestVerb"/> + <paramref name="path"/>.
        /// Verb is checked first (cheap O(n) string compare) so a mismatch
        /// short-circuits before the regex runs. On <c>true</c>,
        /// <paramref name="captures"/> contains every named group
        /// (<c>(?&lt;name&gt;…)</c>) that participated in the match —
        /// surfaced to binding via <see cref="BindingSource.Path"/>.
        /// On <c>false</c>, <paramref name="captures"/> is empty.
        /// </summary>
        public bool TryMatch(string requestVerb, string path,
            out IReadOnlyDictionary<string, string> captures)
        {
            if (!MatchesVerb(requestVerb))
            {
                captures = EmptyCaptures;
                return false;
            }
            var match = this.pattern.Match(path ?? string.Empty);
            if (!match.Success)
            {
                captures = EmptyCaptures;
                return false;
            }
            captures = BuildCaptureMap(match, this.pattern);
            return true;
        }

        private bool MatchesVerb(string requestVerb)
        {
            foreach (var verb in this.Verbs)
            {
                if (string.Equals(verb, requestVerb, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static readonly IReadOnlyDictionary<string, string> EmptyCaptures
            = new Dictionary<string, string>(0);

        private static IReadOnlyDictionary<string, string> BuildCaptureMap(Match match, Regex pattern)
        {
            var captures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var groupName in pattern.GetGroupNames())
            {
                if (int.TryParse(groupName, out _))
                    continue;
                var group = match.Groups[groupName];
                if (!group.Success)
                    continue;
                captures[groupName] = group.Value;
            }
            return captures;
        }
    }
}
