using System;
using System.Reflection;
using System.Text;

namespace EastFive.Api
{
    /// <summary>
    /// Attribute-interface implemented by parameter attributes that need to
    /// contribute to or otherwise modify the URL regex pattern under which
    /// their owning method is matched (template-based routing).
    ///
    /// The framework iterates each parameter attribute that implements this
    /// interface during <see cref="HttpVerbAttribute.GetRouteTemplate"/> and
    /// gives it the pattern-so-far. Every modifier runs, in parameter /
    /// attribute declaration order; each sees the output of the previous
    /// one. An attribute that wants to opt out simply returns the input
    /// string unchanged.
    ///
    /// Decoupling: this replaces the prior <c>TryGetFileNameCaptureKey</c>
    /// helper that was hard-wired to <c>[QueryId]</c>,
    /// <c>[QueryParameter(CheckFileName = true)]</c>, and
    /// <see cref="IProvideBindingRequirements"/>. Each attribute now owns
    /// the regex fragment it wants to inject, and composite-key /
    /// multi-segment routes can layer multiple modifiers cleanly.
    /// </summary>
    public interface IModifyRoutePattern
    {
        /// <summary>
        /// Mutate (or pass through) the route regex pattern in light of this
        /// parameter. Return <paramref name="currentPattern"/> unchanged to
        /// signal "no contribution".
        /// </summary>
        string ModifyRoutePattern(MethodInfo method, ParameterInfo parameter, string currentPattern);
    }

    /// <summary>
    /// Helpers for <see cref="IModifyRoutePattern"/> implementers that just
    /// want to append a single trailing named-capture group (the common
    /// /api/Resource/{id} case).
    /// </summary>
    public static class RoutePattern
    {
        /// <summary>
        /// Regex alternation matching a GUID in 32-digit ("N") or hyphenated
        /// ("D") form — the two shapes that appear in a URL path segment.
        /// </summary>
        public const string GuidSegmentPattern =
            "[0-9a-fA-F]{32}|[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}";

        /// <summary>
        /// Regex body for a single URL path segment (no '/'). Trailing captures
        /// are constrained to one segment so a route with a trailing string key
        /// (e.g. <c>/auth/SAMLRedirect/{tag}</c>) cannot swallow a sibling
        /// action route's deeper path (e.g. <c>/auth/SAMLRedirect/logout/{tag}</c>).
        /// </summary>
        public const string SingleSegmentPattern = "[^/]+";

        /// <summary>
        /// Append <c>(?:/(?&lt;key&gt;[^/]+))?</c> to <paramref name="current"/>.
        /// Returns <paramref name="current"/> unchanged when
        /// <paramref name="rawCaptureName"/> is empty / sanitises to empty.
        /// </summary>
        public static string AppendTrailingCapture(string current, string rawCaptureName)
            => AppendTrailingCapture(current, rawCaptureName, segmentPattern: SingleSegmentPattern);

        /// <summary>
        /// Append a trailing capture whose body is constrained to
        /// <paramref name="keyType"/>'s URL shape: a GUID alternation for
        /// <see cref="Guid"/> / <see cref="IReferenceable"/> (e.g.
        /// <c>IRef&lt;T&gt;</c>) keys, otherwise the permissive <c>.*</c>.
        /// A constrained capture lets a sibling action route
        /// (e.g. <c>/Resource/SpotCheck</c>) win instead of being swallowed by
        /// the generic <c>/Resource/{id}</c> template.
        /// </summary>
        public static string AppendTrailingCapture(string current, string rawCaptureName, Type keyType)
            => AppendTrailingCapture(current, rawCaptureName, SegmentPatternForKeyType(keyType));

        /// <summary>
        /// Append <c>(?:/(?&lt;key&gt;SEGMENT))?</c> to <paramref name="current"/>,
        /// where SEGMENT is <paramref name="segmentPattern"/> (falling back to
        /// <see cref="SingleSegmentPattern"/> when null/empty). Returns
        /// <paramref name="current"/> unchanged when
        /// <paramref name="rawCaptureName"/> sanitises to empty.
        /// </summary>
        public static string AppendTrailingCapture(string current, string rawCaptureName, string segmentPattern)
        {
            var captureName = SanitizeCaptureName(rawCaptureName);
            if (string.IsNullOrEmpty(captureName))
                return current;
            var segment = string.IsNullOrEmpty(segmentPattern) ? SingleSegmentPattern : segmentPattern;
            return current + "(?:/(?<" + captureName + ">" + segment + "))?";
        }

        /// <summary>
        /// Map a key/identifier type to the regex body that matches its URL
        /// segment. GUID-backed identifiers — <see cref="Guid"/>, nullable
        /// <see cref="Guid"/>, and references implementing
        /// <see cref="IReferenceable"/> / <see cref="IReferenceableOptional"/>
        /// (such as <c>IRef&lt;T&gt;</c> / <c>IRefOptional&lt;T&gt;</c>) —
        /// constrain to <see cref="GuidSegmentPattern"/>; everything else is a
        /// single path segment (<see cref="SingleSegmentPattern"/>).
        /// </summary>
        public static string SegmentPatternForKeyType(Type keyType)
        {
            if (keyType == null)
                return SingleSegmentPattern;
            if (keyType == typeof(Guid) || keyType == typeof(Guid?))
                return GuidSegmentPattern;
            if (typeof(IReferenceable).IsAssignableFrom(keyType))
                return GuidSegmentPattern;
            if (typeof(IReferenceableOptional).IsAssignableFrom(keyType))
                return GuidSegmentPattern;
            return SingleSegmentPattern;
        }

        /// <summary>
        /// Strip characters not legal in a .NET regex named-capture group
        /// (keeps letters, digits, underscore; prefixes a leading underscore
        /// when the result starts with a digit).
        /// </summary>
        public static string SanitizeCaptureName(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return null;
            var buf = new StringBuilder(raw.Length);
            foreach (var c in raw)
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                    buf.Append(c);
            }
            if (buf.Length == 0)
                return null;
            if (char.IsDigit(buf[0]))
                buf.Insert(0, '_');
            return buf.ToString();
        }
    }
}
