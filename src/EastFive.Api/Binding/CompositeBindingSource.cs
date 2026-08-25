using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using EastFive.Serialization.Binding;

namespace EastFive.Api.Binding
{
    /// <summary>
    /// The single <see cref="IBindingSource"/> the V3 bind phase consumes. Built
    /// at the end of method selection from each matched <see cref="IBindFromRequest"/>
    /// parameter's <see cref="BindCall"/>, keyed by parameter name.
    /// <para>
    /// Navigation:
    /// <list type="bullet">
    ///   <item>Empty path → <c>onObject(this)</c>.</item>
    ///   <item>Otherwise: split head segment, look up the parameter's
    ///   <see cref="BindCall"/>, invoke it with the remainder path.</item>
    ///   <item>Unknown head segment → <see cref="NotPresent"/>.</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>TResult contract.</b> Per V3 plan decision A, this source supports only
    /// <c>TResult = object</c> — the type the V3 dispatcher's
    /// <c>TypeBindings.Bind&lt;object&gt;(…)</c> call threads down through every
    /// binder. Other <c>TResult</c> values throw, surfacing misuse loudly.
    /// </para>
    /// </summary>
    public sealed class CompositeBindingSource : IBindingSource
    {
        private readonly IReadOnlyDictionary<string, BindCall> members;

        public CompositeBindingSource(IReadOnlyDictionary<string, BindCall> members)
        {
            this.members = members ?? throw new ArgumentNullException(nameof(members));
        }

        public bool HasMember(string name) => members.ContainsKey(name);

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
            if (typeof(TResult) != typeof(object))
                throw new InvalidOperationException(
                    $"CompositeBindingSource only supports TResult = object; got `{typeof(TResult).FullName}`. " +
                    "The V3 dispatcher must invoke TypeBindings.Bind<object>(…) on this source.");

            if (string.IsNullOrEmpty(path))
            {
                if (onObject is not null) return new ValueTask<TResult>(onObject(this));
                return BindingSourceDispatch.WrongType<TResult>(
                    BindingSourceDispatch.InferExpected(
                        hasString: onString is not null, hasGuid: onGuid is not null, hasBool: onBool is not null,
                        hasInt64: onInt64 is not null, hasDouble: onDouble is not null,
                        hasDateTime: onDateTime is not null, hasBytes: onBytes is not null,
                        hasObject: false, hasArray: onArray is not null),
                    "object", typeof(object), path, onFailure);
            }

            var (head, rest) = SplitHead(path);
            if (!members.TryGetValue(head, out var call))
                return BindingSourceDispatch.FailTask<TResult>(
                    new BindFailure(new NotPresent(), typeof(object), path), onFailure);

            // Safe casts: typeof(TResult) == typeof(object) checked above, so every
            // Func<…, TResult> is structurally a Func<…, object> and the returned
            // ValueTask<object> can be reinterpreted as ValueTask<TResult>.
            var task = call(
                rest,
                (Func<object>)(object)onNull,
                (Func<string, object>)(object)onString,
                (Func<Guid, object>)(object)onGuid,
                (Func<bool, object>)(object)onBool,
                (Func<long, object>)(object)onInt64,
                (Func<double, object>)(object)onDouble,
                (Func<DateTime, object>)(object)onDateTime,
                (Func<byte[], object>)(object)onBytes,
                (Func<IBindingSource, object>)(object)onObject,
                (Func<IEnumerableBindingSource, object>)(object)onArray,
                elementTypeHint,
                (Func<BindFailure, object>)(object)onFailure);
            return (ValueTask<TResult>)(object)task;
        }

        private static (string head, string rest) SplitHead(string path)
        {
            for (var i = 0; i < path.Length; i++)
            {
                var c = path[i];
                if (c == '.') return (path.Substring(0, i), path.Substring(i + 1));
                if (c == '[') return (path.Substring(0, i), path.Substring(i));
            }
            return (path, string.Empty);
        }
    }
}
