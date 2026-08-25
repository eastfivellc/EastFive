using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using EastFive.Serialization;
using EastFive.Serialization.Binding;
using EastFive.Serialization.Binding.Sources;

namespace EastFive.Api.Serialization.Binding.Sources
{
    /// <summary>
    /// <see cref="IBindingSource"/> over a flat, multi-valued string lookup — the
    /// canonical shape of both ASP.NET query strings and form posts. Composes
    /// <c>path</c> onto the source's current prefix and probes what exists:
    /// <list type="bullet">
    ///   <item>exact leaf key, single value → <c>onString</c> (delegating to
    ///   <see cref="StringBindingSource"/>'s null sentinels).</item>
    ///   <item>multi-valued key OR <c>[i]</c>-indexed siblings → <c>onArray</c>.</item>
    ///   <item>named child branches exist but no exact value → <c>onObject</c>.</item>
    ///   <item>nothing matches → <see cref="NotPresent"/>.</item>
    /// </list>
    /// Key lookups are case-insensitive (ASP.NET conventional).
    /// </summary>
    public sealed class LookupBindingSource : IBindingSource
    {
        private readonly IReadOnlyDictionary<string, string[]> values;
        private readonly string prefix;
        private readonly CultureInfo culture;

        public LookupBindingSource(IDictionary<string, string[]> values, CultureInfo culture = null)
            : this(Materialize(values), string.Empty, culture)
        { }

        public LookupBindingSource(IEnumerable<KeyValuePair<string, string[]>> values, CultureInfo culture = null)
            : this(Materialize(values), string.Empty, culture)
        { }

        private LookupBindingSource(IReadOnlyDictionary<string, string[]> values, string prefix, CultureInfo culture)
        {
            this.values = values;
            this.prefix = prefix ?? string.Empty;
            this.culture = culture ?? CultureInfo.InvariantCulture;
        }

        private static IReadOnlyDictionary<string, string[]> Materialize(IEnumerable<KeyValuePair<string, string[]>> input)
        {
            var d = new Dictionary<string, string[]>(StringComparer.Ordinal);
            if (input is null) return d;
            foreach (var kv in input)
                d[NormalizeKey(kv.Key)] = kv.Value ?? Array.Empty<string>();
            return d;
        }

        /// <summary>
        /// Normalize alphabetic <c>[name]</c> segments to <c>.name</c> so callers
        /// using dot-composition (PocoBinder) and clients using bracket-composition
        /// (legacy form posts) both hit the same key. Numeric brackets <c>[0]</c>
        /// stay intact — they signal an array index, not a property name.
        /// </summary>
        private static string NormalizeKey(string key)
        {
            if (string.IsNullOrEmpty(key) || key.IndexOf('[') < 0) return key;
            var sb = new System.Text.StringBuilder(key.Length);
            var i = 0;
            while (i < key.Length)
            {
                if (key[i] == '[')
                {
                    var close = key.IndexOf(']', i + 1);
                    if (close < 0) { sb.Append(key, i, key.Length - i); break; }
                    var inside = key.Substring(i + 1, close - i - 1);
                    if (int.TryParse(inside, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                    {
                        sb.Append(key, i, close - i + 1);
                    }
                    else
                    {
                        if (sb.Length > 0 && sb[sb.Length - 1] != '.') sb.Append('.');
                        sb.Append(inside);
                    }
                    i = close + 1;
                    continue;
                }
                sb.Append(key[i]);
                i++;
            }
            return sb.ToString();
        }

        public ValueTask<TResult> GetValue<TResult>(
            string path = null,
            Func<TResult> onNull = null,
            Func<string, TResult> onString = null,
            Func<Guid, TResult> onGuid = null,
            Func<bool, TResult> onBool = null,
            Func<long, TResult> onInt64 = null,
            Func<double, TResult> onDouble = null,
            Func<DateTime, TResult> onDateTime = null,
            Func<byte[], TResult> onBytes = null,
            Func<IBindingSource, TResult> onObject = null,
            Func<IEnumerableBindingSource, TResult> onArray = null,
            Type elementTypeHint = null,
            Func<BindFailure, TResult> onFailure = null)
        {
            var composedPrefix = ComposePrefix(prefix, path);

            // 1. Multi-valued leaf — string array form (?ids=a&ids=b).
            if (TryGetLeaf(composedPrefix, out var vals))
            {
                if (vals.Length > 1)
                {
                    if (onArray is not null)
                    {
                        var sources = vals.Select(s => (IBindingSource)new StringBindingSource(s, culture));
                        return new ValueTask<TResult>(onArray(new EnumerableBindingSource(sources, elementTypeHint ?? typeof(string))));
                    }
                    // Multi-value where only scalar expected: surface first value.
                    return DelegateScalar(vals[0], onNull, onString, onGuid, onBool, onInt64, onDouble,
                        onDateTime, onBytes, onObject, onArray, elementTypeHint, onFailure);
                }
                // Single value — may be a scalar OR a comma/semicolon-separated array.
                if (onArray is not null)
                {
                    var split = EastFive.Serialization.StringExtensions.ParseStringToArray(vals[0]);
                    var sources = split.Select(s => (IBindingSource)new StringBindingSource(s, culture));
                    return new ValueTask<TResult>(onArray(new EnumerableBindingSource(sources, elementTypeHint ?? typeof(string))));
                }
                return DelegateScalar(vals[0], onNull, onString, onGuid, onBool, onInt64, onDouble,
                    onDateTime, onBytes, onObject, onArray, elementTypeHint, onFailure);
            }

            // 2. Numeric-indexed children (foo[0], foo[1], …).
            var indexed = EnumerateIndexedChildren(composedPrefix).ToArray();
            if (indexed.Length > 0)
            {
                if (onArray is not null)
                    return new ValueTask<TResult>(onArray(new EnumerableBindingSource(indexed, elementTypeHint)));
                if (onObject is not null)
                    return new ValueTask<TResult>(onObject(new LookupBindingSource(values, composedPrefix, culture)));
                return BindingSourceDispatch.WrongType<TResult>("array", "indexed-lookup", typeof(object), path, onFailure);
            }

            // 3. Named child branches exist.
            if (HasDescendants(composedPrefix))
            {
                if (onObject is not null)
                    return new ValueTask<TResult>(onObject(new LookupBindingSource(values, composedPrefix, culture)));
                return BindingSourceDispatch.WrongType<TResult>("object", "lookup-branch", typeof(object), path, onFailure);
            }

            // 4. Nothing matches.
            return BindingSourceDispatch.FailTask(
                new BindFailure(new NotPresent(), typeof(object), composedPrefix), onFailure);
        }

        // ---- composition helpers ----

        private static string ComposePrefix(string current, string addition)
        {
            if (string.IsNullOrEmpty(addition)) return current ?? string.Empty;
            if (string.IsNullOrEmpty(current))
            {
                // Bracketed-leading additions are root-anchored.
                if (addition[0] == '[' || addition[0] == '.') return addition;
                return addition;
            }
            if (addition[0] == '[') return current + addition;
            if (addition[0] == '.') return current + addition;
            return current + "." + addition;
        }

        private bool TryGetLeaf(string key, out string[] vals)
        {
            if (string.IsNullOrEmpty(key)) { vals = null; return false; }
            if (values.TryGetValue(key, out vals)) return vals is not null && vals.Length > 0;
            foreach (var kv in values)
            {
                if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    vals = kv.Value;
                    return vals is not null && vals.Length > 0;
                }
            }
            vals = null;
            return false;
        }

        private bool HasDescendants(string p)
        {
            foreach (var k in values.Keys)
            {
                if (p.Length == 0)
                {
                    if (k.Length > 0) return true;
                    continue;
                }
                if (k.Length <= p.Length) continue;
                if (!k.StartsWith(p, StringComparison.OrdinalIgnoreCase)) continue;
                var next = k[p.Length];
                if (next == '.' || next == '[') return true;
            }
            return false;
        }

        private IEnumerable<IBindingSource> EnumerateIndexedChildren(string p)
        {
            var indices = new SortedSet<int>();
            foreach (var k in values.Keys)
            {
                if (p.Length == 0)
                {
                    if (k.Length < 3 || k[0] != '[') continue;
                    var close0 = k.IndexOf(']');
                    if (close0 < 2) continue;
                    var inside0 = k.AsSpan(1, close0 - 1);
                    if (!int.TryParse(inside0, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i0)) continue;
                    indices.Add(i0);
                    continue;
                }
                if (k.Length <= p.Length + 2) continue;
                if (!k.StartsWith(p, StringComparison.OrdinalIgnoreCase)) continue;
                if (k[p.Length] != '[') continue;
                var close = k.IndexOf(']', p.Length + 1);
                if (close < 0) continue;
                var inside = k.AsSpan(p.Length + 1, close - p.Length - 1);
                if (!int.TryParse(inside, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) continue;
                indices.Add(i);
            }
            foreach (var i in indices)
                yield return new LookupBindingSource(values, p.Length == 0 ? $"[{i}]" : $"{p}[{i}]", culture);
        }

        private ValueTask<TResult> DelegateScalar<TResult>(
            string scalar,
            Func<TResult> onNull,
            Func<string, TResult> onString,
            Func<Guid, TResult> onGuid,
            Func<bool, TResult> onBool,
            Func<long, TResult> onInt64,
            Func<double, TResult> onDouble,
            Func<DateTime, TResult> onDateTime,
            Func<byte[], TResult> onBytes,
            Func<IBindingSource, TResult> onObject,
            Func<IEnumerableBindingSource, TResult> onArray,
            Type elementTypeHint,
            Func<BindFailure, TResult> onFailure)
        {
            // Lookup values are always strings; route through StringBindingSource so
            // null-sentinel handling stays consistent.
            return new StringBindingSource(scalar, culture).GetValue(
                path: null,
                onNull: onNull,
                onString: onString,
                onGuid: onGuid,
                onBool: onBool,
                onInt64: onInt64,
                onDouble: onDouble,
                onDateTime: onDateTime,
                onBytes: onBytes,
                onObject: onObject,
                onArray: onArray,
                elementTypeHint: elementTypeHint,
                onFailure: onFailure);
        }
    }
}
