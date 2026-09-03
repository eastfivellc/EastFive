using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EastFive.Serialization.Binding.Binders
{
    /// <summary>
    /// Binds <c>object</c>-typed targets from whatever native shape the source
    /// holds: natives (string/long/double/bool/Guid/DateTime/byte[]) box as-is
    /// with no coercion, a nested object re-binds as
    /// <c>IDictionary&lt;string, object&gt;</c> and a nested array as
    /// <c>object[]</c> — both through <see cref="IBindingContext.TypeBindings"/>,
    /// so nesting recurses through <see cref="DictionaryBinder"/> /
    /// <see cref="ArrayBinder"/> back into this binder. Without it, an
    /// <c>object</c> target fell to <see cref="PocoBinder"/>, which supplies only
    /// onNull/onObject — so any JSON native failed with
    /// <see cref="WrongSourceType"/> ("object", "String") — the
    /// <c>Property&lt;IDictionary&lt;string, object&gt;&gt;</c> body-parameter
    /// failure.
    /// </summary>
    public sealed class ObjectBinder : ITypeBinder
    {
        public bool CanBind(Type targetType) => targetType == typeof(object);

        public async ValueTask<TResult> Read<TResult>(
            Type targetType,
            IBindingSource source,
            IBindingContext context,
            Func<object, TResult> onBound,
            Func<BindFailure, TResult> onFailure,
            Func<TResult> onNull = null)
        {
            var path = context?.KeyPath ?? string.Empty;

            object boxed = null;
            var isNull = false;
            var isObject = false;
            var isArray = false;
            BindFailure? failure = null;

            await source.GetValue<object>(
                path: path,
                onNull: () => { isNull = true; return null; },
                onString: s => { boxed = s; return null; },
                onGuid: g => { boxed = g; return null; },
                onBool: b => { boxed = b; return null; },
                onInt64: i => { boxed = i; return null; },
                onDouble: d => { boxed = d; return null; },
                onDateTime: dt => { boxed = dt; return null; },
                onBytes: bytes => { boxed = bytes; return null; },
                onObject: _ => { isObject = true; return null; },
                onArray: _ => { isArray = true; return null; },
                elementTypeHint: typeof(object),
                onFailure: f => { failure = f; return null; });

            if (failure is { } nf)
            {
                if (nf.Reason is NotPresent && onNull is not null)
                    return onNull();
                return onFailure(nf);
            }

            if (isNull)
            {
                if (onNull is not null)
                    return onNull();
                return onBound(null);
            }

            // Object / array shapes re-bind at the same path so the registered
            // Dictionary/Array binders own navigation and per-element binding.
            if (isObject)
                return await context.TypeBindings.Bind(
                    typeof(IDictionary<string, object>), source, context, onBound, onFailure, onNull);

            if (isArray)
                return await context.TypeBindings.Bind(
                    typeof(object[]), source, context, onBound, onFailure, onNull);

            return onBound(boxed);
        }

        public void Write(Type sourceType, object value, IBindingSink sink, IBindingContext context)
        {
            if (value is null) { sink.WriteNull(); return; }
            // Emit by the value's runtime type — typeof(object) says nothing.
            context.TypeBindings.Emit(value.GetType(), value, sink, context);
        }
    }
}
