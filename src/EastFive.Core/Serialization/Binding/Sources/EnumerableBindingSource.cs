using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace EastFive.Serialization.Binding.Sources
{
    /// <summary>
    /// Default <see cref="IEnumerableBindingSource"/>: wraps an
    /// <see cref="IEnumerable{IBindingSource}"/> + element type. Self-rooted —
    /// non-empty <c>path</c> tries to descend into a numeric-bracketed index
    /// (e.g. <c>[3]</c>), otherwise fails with <see cref="WrongSourceType"/>.
    /// </summary>
    public sealed class EnumerableBindingSource : IEnumerableBindingSource
    {
        private readonly IReadOnlyList<IBindingSource> elements;

        public EnumerableBindingSource(IEnumerable<IBindingSource> elements, Type elementType)
        {
            this.elements = elements as IReadOnlyList<IBindingSource> ?? elements?.ToArray() ?? Array.Empty<IBindingSource>();
            ElementType = elementType;
        }

        public Type ElementType { get; }

        public IEnumerator<IBindingSource> GetEnumerator() => elements.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

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
            if (string.IsNullOrEmpty(path))
            {
                if (onArray is not null) return new ValueTask<TResult>(onArray(this));
                return BindingSourceDispatch.WrongType<TResult>("array", "array", typeof(object), path, onFailure);
            }

            // Try numeric-bracketed head segment: "[N]" or "[N].rest" or "[N][rest]".
            if (PathParser.TryConsumeIndex(path, out var index, out var rest))
            {
                if (index < 0 || index >= elements.Count)
                    return BindingSourceDispatch.FailTask(
                        new BindFailure(new NotPresent(), typeof(object), path), onFailure);
                return elements[index].GetValue(
                    rest, onNull, onString, onGuid, onBool, onInt64, onDouble,
                    onDateTime, onBytes, onObject, onArray, elementTypeHint, onFailure);
            }

            return BindingSourceDispatch.WrongType<TResult>(
                "indexed-array path", "named path on array", typeof(object), path, onFailure);
        }
    }

    /// <summary>
    /// Minimal dotted/bracketed path tokenizer used by composite sources. Recognizes:
    /// <c>foo</c>, <c>foo.bar</c>, <c>foo[3]</c>, <c>foo[bar]</c>, <c>[3]</c>,
    /// <c>[3].bar</c>, <c>[3][4]</c>.
    /// </summary>
    public static class PathParser
    {
        /// <summary>Returns true if <c>path</c> is null or empty.</summary>
        public static bool IsEmpty(string path) => string.IsNullOrEmpty(path);

        /// <summary>
        /// If <c>path</c> begins with a name segment (an identifier-like run not starting
        /// with '['), yields the name and the remainder (after consuming the trailing
        /// '.' if present). Returns false for empty or bracket-leading paths.
        /// </summary>
        public static bool TryConsumeName(string path, out string name, out string rest)
        {
            name = null;
            rest = null;
            if (string.IsNullOrEmpty(path)) return false;
            if (path[0] == '[' || path[0] == '.') return false;
            var i = 0;
            while (i < path.Length && path[i] != '.' && path[i] != '[') i++;
            name = path.Substring(0, i);
            if (i >= path.Length) { rest = string.Empty; return true; }
            if (path[i] == '.') { rest = path.Substring(i + 1); return true; }
            // bracketed continuation stays in `rest` (caller will see the '[')
            rest = path.Substring(i);
            return true;
        }

        /// <summary>
        /// If <c>path</c> begins with <c>[N]</c> (numeric index), yields the index and the
        /// remainder (after consuming any trailing '.').
        /// </summary>
        public static bool TryConsumeIndex(string path, out int index, out string rest)
        {
            index = -1;
            rest = null;
            if (string.IsNullOrEmpty(path) || path[0] != '[') return false;
            var close = path.IndexOf(']');
            if (close < 2) return false;
            var inside = path.Substring(1, close - 1);
            if (!int.TryParse(inside, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) return false;
            index = n;
            if (close + 1 >= path.Length) { rest = string.Empty; return true; }
            if (path[close + 1] == '.') { rest = path.Substring(close + 2); return true; }
            rest = path.Substring(close + 1);
            return true;
        }

        /// <summary>
        /// If <c>path</c> begins with <c>[name]</c> (non-numeric bracketed key), yields
        /// the name and the remainder (after consuming any trailing '.').
        /// </summary>
        public static bool TryConsumeBracketName(string path, out string name, out string rest)
        {
            name = null;
            rest = null;
            if (string.IsNullOrEmpty(path) || path[0] != '[') return false;
            var close = path.IndexOf(']');
            if (close < 2) return false;
            var inside = path.Substring(1, close - 1);
            if (int.TryParse(inside, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) return false;
            name = inside;
            if (close + 1 >= path.Length) { rest = string.Empty; return true; }
            if (path[close + 1] == '.') { rest = path.Substring(close + 2); return true; }
            rest = path.Substring(close + 1);
            return true;
        }
    }
}
