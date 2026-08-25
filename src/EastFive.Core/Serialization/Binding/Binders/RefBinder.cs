using System;
using System.Threading.Tasks;

namespace EastFive.Serialization.Binding.Binders
{
    /// <summary>
    /// Binds <c>IRef&lt;T&gt;</c>. Accepts a native Guid, a native string
    /// (parsed as Guid), or a nested object carrying an <c>id</c> property
    /// (the shape produced by Newtonsoft's default serialization of
    /// <c>Ref&lt;T&gt;</c>, which V2's <c>BindConvert</c> also accepted —
    /// preserves V2/V3 wire compatibility on inbound bodies).
    /// </summary>
    public sealed class RefBinder : ITypeBinder
    {
        public bool CanBind(Type targetType) =>
            targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(IRef<>);

        public async ValueTask<TResult> Read<TResult>(
            Type targetType,
            IBindingSource source,
            IBindingContext context,
            Func<object, TResult> onBound,
            Func<BindFailure, TResult> onFailure,
            Func<TResult> onNull = null)
        {
            var path = context?.KeyPath;
            var referenced = targetType.GetGenericArguments()[0];

            // Probe with onObject fallback to detect the nested {id: guid} shape.
            IBindingSource child = null;
            BindFailure? failure = null;
            var handled = false;
            TResult result = default;

            result = await source.GetValue<TResult>(
                path: path,
                onNull: onNull,
                onGuid: g => { handled = true; return onBound(g.BindToRef(referenced)); },
                onString: s =>
                {
                    handled = true;
                    if (Guid.TryParse(s, out var g))
                        return onBound(g.BindToRef(referenced));
                    return onFailure(new BindFailure(new ParseError($"'{s}' is not a Guid"), targetType, path));
                },
                onObject: src => { child = src; return default; },
                onFailure: f => { failure = f; return onFailure(f); });

            if (handled || failure is not null || child is null)
                return result;

            // Nested object shape — read the "id" sub-path as Guid/string.
            return await child.GetValue<TResult>(
                path: "id",
                onNull: onNull,
                onGuid: g => onBound(g.BindToRef(referenced)),
                onString: s => Guid.TryParse(s, out var g)
                    ? onBound(g.BindToRef(referenced))
                    : onFailure(new BindFailure(new ParseError($"'{s}' is not a Guid"), targetType, path)),
                onFailure: onFailure);
        }

        public void Write(Type sourceType, object value, IBindingSink sink, IBindingContext context)
        {
            if (value is null) { sink.WriteNull(); return; }
            var guidProp = value.GetType().GetProperty("id");
            var g = (Guid)guidProp.GetValue(value);
            sink.WriteGuid(g);
        }
    }
}
