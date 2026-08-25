using System;
using System.Globalization;
using System.Threading.Tasks;

namespace EastFive.Serialization.Binding.Binders
{
    /// <summary>
    /// Binds <see cref="TimeSpan"/>. Accepts string (constant "c" format, e.g. "10:30:00"),
    /// int64 (ticks, mirroring <see cref="DateTimeBinder"/>), or double (seconds). Writes
    /// the constant format string, matching JSON serialization of TimeSpan members.
    /// </summary>
    public sealed class TimeSpanBinder : ITypeBinder
    {
        public bool CanBind(Type targetType) => targetType == typeof(TimeSpan);

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
                    if (TimeSpan.TryParse(s, CultureInfo.InvariantCulture, out var ts))
                        return onBound(ts);
                    return onFailure(new BindFailure(new ParseError($"'{s}' is not a TimeSpan"), typeof(TimeSpan), path));
                },
                onInt64: ticks => onBound(TimeSpan.FromTicks(ticks)),
                onDouble: seconds => onBound(TimeSpan.FromSeconds(seconds)),
                onFailure: onFailure);
        }

        public void Write(Type sourceType, object value, IBindingSink sink, IBindingContext context)
        {
            if (value is null) { sink.WriteNull(); return; }
            sink.WriteString(((TimeSpan)value).ToString("c", CultureInfo.InvariantCulture));
        }
    }
}
