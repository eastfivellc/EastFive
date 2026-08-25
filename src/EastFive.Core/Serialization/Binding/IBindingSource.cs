using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EastFive.Serialization.Binding
{
    /// <summary>
    /// Format-neutral, async-shaped read accessor for a single value. Each format
    /// (string, JSON, EDM/EntityProperty, form/query) implements one method:
    /// <see cref="GetValue{TResult}"/>. The source navigates to <c>path</c> and
    /// dispatches on whatever native type it actually holds there, invoking the
    /// matching callback.
    /// <para>
    /// <b>Sources do not coerce.</b> A source that holds an Int64 calls
    /// <c>onInt64</c>; if no <c>onInt64</c> callback was supplied, it does NOT
    /// fall back to <c>onString</c> — it surfaces a <see cref="WrongSourceType"/>
    /// failure. Cross-type conversion (Guid.TryParse on a string, Int.Parse, base64
    /// decode, etc.) is the binder's responsibility — the binder declares which
    /// native shapes it accepts by supplying those callbacks.
    /// </para>
    /// <para>
    /// All callbacks are optional. A missing callback for the native type at <c>path</c>
    /// invokes <c>onFailure(WrongSourceType)</c>; if <c>onFailure</c> is itself null,
    /// the source THROWS a <see cref="BindFailureException"/>. This lets callers
    /// omit failure handling when they know the shape, and keeps the parameter list
    /// readable with <c>path</c> first and <c>onFailure</c> last.
    /// </para>
    /// <para>
    /// <b>Path syntax:</b> dotted (<c>foo.bar</c>) and bracketed (<c>foo[0]</c>,
    /// <c>foo[bar]</c>). Each source supports the subset that makes sense; a source
    /// that doesn't model nesting (e.g. a raw string value) returns a
    /// <see cref="WrongSourceType"/> failure on any non-empty path.
    /// </para>
    /// <para>
    /// <b>onObject / onArray</b> deliver the navigated child source — they replace
    /// the older <c>GetScoped</c> by folding scoping into <c>path</c>. The
    /// <c>onArray</c> callback receives an <see cref="IEnumerableBindingSource"/>
    /// (an array that is itself an <see cref="IBindingSource"/> carrying an
    /// <see cref="IEnumerableBindingSource.ElementType"/>).
    /// </para>
    /// <para>
    /// <c>elementTypeHint</c> is meaningful only when <c>onArray</c> is supplied
    /// against a source that stores arrays as a single packed primitive (e.g. EDM
    /// binary cells of packed Guid / Int / Long / Double / DateTime / UTF-8 strings).
    /// Other sources ignore it.
    /// </para>
    /// </summary>
    public interface IBindingSource
    {
        ValueTask<TResult> GetValue<TResult>(
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
            Func<BindFailure, TResult> onFailure = null);
    }

    /// <summary>
    /// Array binding source: yields one <see cref="IBindingSource"/> per element
    /// when enumerated, and is itself an <see cref="IBindingSource"/> so the same
    /// callback set can navigate into it. <see cref="ElementType"/> echoes the hint
    /// the source used to materialize its elements (or the natively-known element
    /// type for self-describing array sources).
    /// </summary>
    public interface IEnumerableBindingSource : IBindingSource, IEnumerable<IBindingSource>
    {
        Type ElementType { get; }
    }

    /// <summary>
    /// Optional capability surfaced by a source's <c>onObject</c> child when that
    /// child natively models a key/value bag (a JSON object, a form-collection
    /// group, etc.). <see cref="Keys"/> lists the child keys present at that
    /// source; each key is then a valid <c>path</c> for a subsequent
    /// <see cref="IBindingSource.GetValue{TResult}"/> call on the SAME source
    /// instance. Lets <c>DictionaryBinder</c> materialize
    /// <c>IDictionary&lt;string, TValue&gt;</c> targets without any binder
    /// inspecting the underlying wire format — sources that don't model an
    /// enumerable key set simply don't implement this interface, and
    /// <c>DictionaryBinder</c> reports <see cref="UnsupportedTargetType"/> for
    /// that shape.
    /// </summary>
    public interface IKeyedBindingSource : IBindingSource
    {
        IEnumerable<string> Keys { get; }
    }

    /// <summary>
    /// Thrown by <see cref="IBindingSource.GetValue{TResult}"/> when the caller did
    /// not supply an <c>onFailure</c> callback and a failure occurs. The
    /// <see cref="Failure"/> property carries the structured reason so a catch site
    /// can still inspect kind, expected type, and key path.
    /// </summary>
    public sealed class BindFailureException : Exception
    {
        public BindFailureException(BindFailure failure)
            : base(failure.ToString())
        {
            Failure = failure;
        }

        public BindFailure Failure { get; }
    }

    /// <summary>
    /// Static helpers that produce the canonical "missing callback / null onFailure"
    /// outcome. Used by every source implementation so the rule is enforced in one
    /// place: missing native-type callback → <c>WrongSourceType</c>; null
    /// <c>onFailure</c> → throw.
    /// </summary>
    public static class BindingSourceDispatch
    {
        public static TResult Fail<TResult>(BindFailure failure, Func<BindFailure, TResult> onFailure)
        {
            if (onFailure is not null) return onFailure(failure);
            throw new BindFailureException(failure);
        }

        public static ValueTask<TResult> FailTask<TResult>(BindFailure failure, Func<BindFailure, TResult> onFailure) =>
            new(Fail(failure, onFailure));

        /// <summary>Source represents an explicit null and the caller did not supply onNull.</summary>
        public static ValueTask<TResult> Null<TResult>(
            Type expected,
            string path,
            Func<TResult> onNull,
            Func<BindFailure, TResult> onFailure)
        {
            if (onNull is not null) return new ValueTask<TResult>(onNull());
            return FailTask(new BindFailure(new NullValue(), expected, path ?? string.Empty), onFailure);
        }

        /// <summary>The native type at <c>path</c> has no matching callback supplied.</summary>
        public static ValueTask<TResult> WrongType<TResult>(
            string expectedKinds,
            string got,
            Type targetType,
            string path,
            Func<BindFailure, TResult> onFailure) =>
            FailTask(new BindFailure(new WrongSourceType(expectedKinds, got), targetType, path ?? string.Empty), onFailure);

        /// <summary>
        /// Source-side helper that infers the "expected" kind from which callbacks
        /// the caller supplied. Reports the first supplied callback (binders usually
        /// only supply one or two and the first one is the primary target).
        /// </summary>
        public static string InferExpected(
            bool hasString = false,
            bool hasGuid = false,
            bool hasBool = false,
            bool hasInt64 = false,
            bool hasDouble = false,
            bool hasDateTime = false,
            bool hasBytes = false,
            bool hasObject = false,
            bool hasArray = false)
        {
            if (hasString) return "string";
            if (hasGuid) return "guid";
            if (hasBool) return "bool";
            if (hasInt64) return "int";
            if (hasDouble) return "double";
            if (hasDateTime) return "datetime";
            if (hasBytes) return "bytes";
            if (hasObject) return "object";
            if (hasArray) return "array";
            return "any";
        }
    }
}
