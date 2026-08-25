using System;
using System.Threading.Tasks;

using EastFive.Api.Serialization.Binding.Sources;
using EastFive.Serialization.Binding;
using EastFive.Serialization.Binding.Sources;

namespace EastFive.Api.Binding
{
    /// <summary>
    /// Non-generic peer of <see cref="IBindingSource.GetValue{TResult}"/>, pinned
    /// to <c>TResult = object</c>. Each V3 selection attribute returns one of
    /// these instead of constructing an <see cref="IBindingSource"/>, letting the
    /// common single-scalar case (Query / Route / Header) ship as a single
    /// closure capturing the captured value rather than two wrapper allocations.
    /// <para>
    /// <b>Why non-generic.</b> The V3 dispatcher only ever calls
    /// <c>TypeBindings.Default.Bind&lt;object&gt;(…)</c> on the composite, so every
    /// downstream binder threads <c>TResult = object</c>. Pinning the delegate to
    /// object trades one box per primitive read (already paid by
    /// <c>Bind&lt;object&gt;</c>) for the ability to express selection as a flat
    /// closure family. <see cref="CompositeBindingSource"/> rejects non-object
    /// <c>TResult</c> at runtime (decision A from the V3 plan).
    /// </para>
    /// <para>
    /// <b>Path semantics.</b> The <paramref name="path"/> argument is the remainder
    /// AFTER <see cref="CompositeBindingSource"/> has stripped the parameter-name
    /// head. Leaf closures (scalars) return <see cref="NotPresent"/> on any
    /// non-empty path; sub-source closures (body) compose path onto a captured
    /// prefix and forward to the underlying <see cref="IBindingSource"/>.
    /// </para>
    /// </summary>
    public delegate ValueTask<object> BindCall(
        string path = null,
        Func<object> onNull = null,
        Func<string, object> onString = null,
        Func<Guid, object> onGuid = null,
        Func<bool, object> onBool = null,
        Func<long, object> onInt64 = null,
        Func<double, object> onDouble = null,
        Func<DateTime, object> onDateTime = null,
        Func<byte[], object> onBytes = null,
        Func<IBindingSource, object> onObject = null,
        Func<IEnumerableBindingSource, object> onArray = null,
        Type elementTypeHint = null,
        Func<BindFailure, object> onFailure = null);

    /// <summary>
    /// Reusable <see cref="BindCall"/> factories. Each one is selection-time hot
    /// path, so they minimize allocations: single-scalar / single-source cases
    /// produce one closure capturing the value, multi-value cases delegate to a
    /// real <see cref="LookupBindingSource"/> only when actually multi.
    /// </summary>
    public static class BindCalls
    {
        /// <summary>Always reports <see cref="NotPresent"/>. Used as the optional-absent contribution.</summary>
        public static readonly BindCall NotPresent = (
            string path,
            Func<object> onNull,
            Func<string, object> onString,
            Func<Guid, object> onGuid,
            Func<bool, object> onBool,
            Func<long, object> onInt64,
            Func<double, object> onDouble,
            Func<DateTime, object> onDateTime,
            Func<byte[], object> onBytes,
            Func<IBindingSource, object> onObject,
            Func<IEnumerableBindingSource, object> onArray,
            Type elementTypeHint,
            Func<BindFailure, object> onFailure) =>
                BindingSourceDispatch.FailTask<object>(
                    new BindFailure(new NotPresent(), typeof(object), path ?? string.Empty),
                    onFailure);

        /// <summary>
        /// Reports an explicit null when the consumer can accept one
        /// (<c>onNull</c> present — i.e. the target is <c>Nullable&lt;T&gt;</c> or a
        /// reference type), otherwise falls back to <see cref="NotPresent"/> so a
        /// C# default value can still apply. Used as the optional-absent
        /// contribution by <c>[QueryOptional]</c>, letting an absent query value
        /// bind to <c>null</c> without requiring a C# default.
        /// </summary>
        public static readonly BindCall Null = (
            string path,
            Func<object> onNull,
            Func<string, object> onString,
            Func<Guid, object> onGuid,
            Func<bool, object> onBool,
            Func<long, object> onInt64,
            Func<double, object> onDouble,
            Func<DateTime, object> onDateTime,
            Func<byte[], object> onBytes,
            Func<IBindingSource, object> onObject,
            Func<IEnumerableBindingSource, object> onArray,
            Type elementTypeHint,
            Func<BindFailure, object> onFailure) =>
                onNull is not null
                    ? new ValueTask<object>(onNull())
                    : BindingSourceDispatch.FailTask<object>(
                        new BindFailure(new NotPresent(), typeof(object), path ?? string.Empty),
                        onFailure);

        /// <summary>
        /// Single string value at the root of the parameter (empty path);
        /// any non-empty path reports <see cref="NotPresent"/>; missing
        /// <c>onString</c> reports <see cref="WrongSourceType"/>.
        /// </summary>
        public static BindCall Scalar(string value) => (
            path, onNull, onString, onGuid, onBool, onInt64, onDouble, onDateTime,
            onBytes, onObject, onArray, elementTypeHint, onFailure) =>
        {
            if (!string.IsNullOrEmpty(path))
                return BindingSourceDispatch.FailTask<object>(
                    new BindFailure(new NotPresent(), typeof(object), path), onFailure);
            if (onString is not null)
                return new ValueTask<object>(onString(value));
            return BindingSourceDispatch.WrongType<object>(
                BindingSourceDispatch.InferExpected(
                    hasString: false, hasGuid: onGuid is not null, hasBool: onBool is not null,
                    hasInt64: onInt64 is not null, hasDouble: onDouble is not null,
                    hasDateTime: onDateTime is not null, hasBytes: onBytes is not null,
                    hasObject: onObject is not null, hasArray: onArray is not null),
                "string", typeof(object), path, onFailure);
        };

        /// <summary>
        /// 0 values → <see cref="NotPresent"/>; 1 value → <see cref="Scalar"/>;
        /// multi → delegates to a <see cref="LookupBindingSource"/> built around
        /// the single (key, values) pair so <c>onArray</c> dispatch matches the
        /// existing semantics for repeated query keys / multi-valued headers.
        /// </summary>
        public static BindCall MultiValue(string key, string[] values)
        {
            if (values is null || values.Length == 0) return NotPresent;
            if (values.Length == 1) return Scalar(values[0]);
            var lookup = new LookupBindingSource(
                new[] { new System.Collections.Generic.KeyValuePair<string, string[]>(key, values) });
            return FromSource(lookup, key);
        }

        /// <summary>
        /// Forwards every call to <paramref name="inner"/>, prepending
        /// <paramref name="prefix"/> to the path. Used by body attributes that
        /// locate the parameter at a path inside a JSON / form root.
        /// </summary>
        public static BindCall FromSource(IBindingSource inner, string prefix)
        {
            var capturedPrefix = prefix ?? string.Empty;
            return (path, onNull, onString, onGuid, onBool, onInt64, onDouble, onDateTime,
                    onBytes, onObject, onArray, elementTypeHint, onFailure) =>
            {
                return inner.GetValue<object>(
                    EnvelopeBodyAccessor.ComposePath(capturedPrefix, path),
                    onNull, onString, onGuid, onBool, onInt64, onDouble, onDateTime, onBytes,
                    onObject, onArray, elementTypeHint, onFailure);
            };
        }
    }
}
