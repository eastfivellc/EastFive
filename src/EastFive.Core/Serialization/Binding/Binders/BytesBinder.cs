using System;
using System.Threading.Tasks;

namespace EastFive.Serialization.Binding.Binders
{
    /// <summary>Binds <c>byte[]</c>. Accepts native bytes or base64 string.</summary>
    public sealed class BytesBinder : ITypeBinder
    {
        public bool CanBind(Type targetType) => targetType == typeof(byte[]);

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
                onBytes: b => onBound(b),
                onString: s =>
                {
                    try { return onBound(Convert.FromBase64String(s)); }
                    catch (FormatException) { return onFailure(new BindFailure(new ParseError("invalid base64"), typeof(byte[]), path)); }
                },
                onFailure: onFailure);
        }

        public void Write(Type sourceType, object value, IBindingSink sink, IBindingContext context)
        {
            if (value is null) { sink.WriteNull(); return; }
            sink.WriteBytes((byte[])value);
        }
    }
}
