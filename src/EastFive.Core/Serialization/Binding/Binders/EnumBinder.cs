using System;
using System.Threading.Tasks;

namespace EastFive.Serialization.Binding.Binders
{
    /// <summary>Binds any <c>enum</c>. Accepts native string (parse) or native int (cast).</summary>
    public sealed class EnumBinder : ITypeBinder
    {
        public bool CanBind(Type targetType) => targetType.IsEnum;

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
                onString: s =>
                {
                    try { return onBound(Enum.Parse(targetType, s, ignoreCase: true)); }
                    catch (ArgumentException) { return onFailure(new BindFailure(new ParseError($"'{s}' is not a {targetType.Name}"), targetType, path)); }
                },
                onInt64: i =>
                {
                    try { return onBound(Enum.ToObject(targetType, i)); }
                    catch (Exception) { return onFailure(new BindFailure(new ParseError($"{i} is not a {targetType.Name}"), targetType, path)); }
                },
                onFailure: onFailure);
        }

        public void Write(Type sourceType, object value, IBindingSink sink, IBindingContext context)
        {
            if (value is null) { sink.WriteNull(); return; }
            sink.WriteString(value.ToString());
        }
    }
}
