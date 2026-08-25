using System;
using System.Globalization;
using System.Threading.Tasks;

namespace EastFive.Serialization.Binding.Binders
{
    /// <summary>Binds <see cref="DateTime"/>. Accepts native datetime, string (parse), or int64 (ticks).</summary>
    public sealed class DateTimeBinder : ITypeBinder
    {
        public bool CanBind(Type targetType) => targetType == typeof(DateTime);

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
                onDateTime: dt => onBound(dt),
                onString: s =>
                {
                    if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                        return onBound(dt);
                    return onFailure(new BindFailure(new ParseError($"'{s}' is not a DateTime"), typeof(DateTime), path));
                },
                onInt64: ticks => onBound(new DateTime(ticks)),
                onFailure: onFailure);
        }

        public void Write(Type sourceType, object value, IBindingSink sink, IBindingContext context)
        {
            if (value is null) { sink.WriteNull(); return; }
            sink.WriteDateTime((DateTime)value);
        }
    }

    /// <summary>Binds <see cref="DateTimeOffset"/>. Accepts native datetime, string (parse), or int64 (ticks).</summary>
    public sealed class DateTimeOffsetBinder : ITypeBinder
    {
        public bool CanBind(Type targetType) => targetType == typeof(DateTimeOffset);

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
                onDateTime: dt => onBound(new DateTimeOffset(dt, TimeSpan.Zero)),
                onString: s =>
                {
                    if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
                        return onBound(dto);
                    return onFailure(new BindFailure(new ParseError($"'{s}' is not a DateTimeOffset"), typeof(DateTimeOffset), path));
                },
                onInt64: ticks => onBound(new DateTimeOffset(new DateTime(ticks), TimeSpan.Zero)),
                onFailure: onFailure);
        }

        public void Write(Type sourceType, object value, IBindingSink sink, IBindingContext context)
        {
            if (value is null) { sink.WriteNull(); return; }
            var dto = (DateTimeOffset)value;
            sink.WriteDateTime(dto.UtcDateTime);
        }
    }
}
