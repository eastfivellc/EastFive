using System;
using System.Threading.Tasks;

namespace EastFive.Serialization.Binding.Binders
{
    /// <summary>Binds <see cref="Guid"/>. Accepts native Guid (passthrough) and native string (parse).</summary>
    public sealed class GuidBinder : ITypeBinder
    {
        public bool CanBind(Type targetType) => targetType == typeof(Guid);

        public ValueTask<TResult> Read<TResult>(
            Type targetType,
            IBindingSource source,
            IBindingContext context,
            Func<object, TResult> onBound,
            Func<BindFailure, TResult> onFailure,
            Func<TResult> onNull = null)
        {
            var path = context?.KeyPath;
            return source.GetValue<TResult>(
                path: path,
                onNull: onNull,
                onGuid: g => onBound((object)g),
                onString: s =>
                {
                    if (Guid.TryParse(s, out var g))
                        return onBound((object)g);
                    return onFailure(new BindFailure(new ParseError($"'{s}' is not a Guid"), typeof(Guid), path));
                },
                onFailure: onFailure);
        }

        public void Write(Type sourceType, object value, IBindingSink sink, IBindingContext context)
        {
            if (value is null) { sink.WriteNull(); return; }
            sink.WriteGuid((Guid)value);
        }
    }
}
