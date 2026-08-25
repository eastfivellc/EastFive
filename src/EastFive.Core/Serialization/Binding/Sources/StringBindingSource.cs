using System;
using System.Globalization;
using System.Threading.Tasks;

namespace EastFive.Serialization.Binding.Sources
{
    /// <summary>
    /// <see cref="IBindingSource"/> over a raw <see cref="string"/>. Always represents
    /// a scalar string value (or null per the legacy sentinels). Non-empty
    /// <c>path</c> is rejected with <see cref="WrongSourceType"/> — a string source
    /// has no nested structure.
    /// <para>
    /// Recognized null sentinels (matching legacy <c>StringExtensions.BindTo</c>):
    /// <c>null</c>, empty/whitespace string, the literal <c>"null"</c> and
    /// <c>"empty"</c> (case-insensitive).
    /// </para>
    /// </summary>
    public sealed class StringBindingSource : IBindingSource
    {
        private readonly string value;
        private readonly CultureInfo culture;

        public StringBindingSource(string value, CultureInfo culture = null)
        {
            this.value = value;
            this.culture = culture ?? CultureInfo.InvariantCulture;
        }

        private bool RepresentsNull =>
            string.IsNullOrWhiteSpace(value) ||
            string.Equals(value, "null", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "empty", StringComparison.OrdinalIgnoreCase);

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
            if (!string.IsNullOrEmpty(path))
                return BindingSourceDispatch.WrongType<TResult>(
                    "scalar", "navigation into string", typeof(object), path, onFailure);

            if (RepresentsNull)
                return BindingSourceDispatch.Null(typeof(string), path, onNull, onFailure);

            if (onString is not null)
                return new ValueTask<TResult>(onString(value));

            return BindingSourceDispatch.WrongType<TResult>("string", "string", typeof(object), path, onFailure);
        }
    }
}
