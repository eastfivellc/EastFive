using System;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace EastFive.Serialization.Parquet
{
    /// <summary>
    /// Compiles, once per pair of types, the conversion from the values a file column holds to the
    /// values a binding's column holds — how a column written by another engine, as text, or with
    /// different nullability still reads into today's member. A conversion that fails for one value
    /// throws, and the reader reports that cell as an <see cref="UnparsedRow"/>.
    /// </summary>
    internal static class ParquetValueConversion
    {
        /// <summary>Null when no conversion exists from <typeparamref name="TIn"/> to <typeparamref name="TOut"/>.</summary>
        public static Func<TIn, TOut> Create<TIn, TOut>()
        {
            if (typeof(TIn) == typeof(TOut))
                return (Func<TIn, TOut>)(object)new Func<TIn, TIn>(value => value);
            var input = Expression.Parameter(typeof(TIn), "value");
            var body = Convert(input, typeof(TOut));
            return body is null
                ? null
                : Expression.Lambda<Func<TIn, TOut>>(body, input).Compile();
        }

        private static Expression Convert(Expression value, Type to)
        {
            var from = value.Type;
            if (from == to)
                return value;
            if (from == typeof(string))
                return FromString(value, to);
            if (to == typeof(string))
                return ToText(value);

            if (Nullable.GetUnderlyingType(from) is Type fromCore)
            {
                var converted = Convert(Expression.Property(value, nameof(Nullable<int>.Value)), to);
                return converted is null
                    ? null
                    : Expression.Condition(
                        Expression.Property(value, nameof(Nullable<int>.HasValue)),
                        converted,
                        Expression.Default(to));
            }
            if (Nullable.GetUnderlyingType(to) is Type toCore)
            {
                var converted = Convert(value, toCore);
                return converted is null ? null : Expression.Convert(converted, to);
            }

            if (from == typeof(DateTime) && to == typeof(DateTimeOffset))
                return Expression.Call(Method(nameof(ToUtcOffset)), value);
            if (from == typeof(DateTimeOffset) && to == typeof(DateTime))
                return Expression.Property(value, nameof(DateTimeOffset.UtcDateTime));
            if (IsNumeric(from) && IsNumeric(to))
                return Expression.ConvertChecked(value, to);
            return null;
        }

        private static Expression FromString(Expression text, Type to)
        {
            var core = Nullable.GetUnderlyingType(to) ?? to;
            var parse = ParseMethod(core);
            if (parse is null)
                return null;
            return Expression.Condition(
                Expression.Call(typeof(string).GetMethod(nameof(string.IsNullOrWhiteSpace), new[] { typeof(string) }), text),
                Expression.Default(to),
                Expression.Convert(Expression.Call(parse, text), to));
        }

        private static MethodInfo ParseMethod(Type type)
        {
            if (type.IsEnum)
                return Method(nameof(ParseEnum)).MakeGenericMethod(type);
            if (type == typeof(DateTime))
                return Method(nameof(ParseDateTime));
            if (type == typeof(DateTimeOffset))
                return Method(nameof(ParseDateTimeOffset));
            if (type == typeof(bool))
                return Method(nameof(ParseBoolean));
            var parsable = typeof(IParsable<>).MakeGenericType(type);
            return parsable.IsAssignableFrom(type)
                ? Method(nameof(ParseParsable)).MakeGenericMethod(type)
                : null;
        }

        private static Expression ToText(Expression value)
        {
            var from = value.Type;
            if (Nullable.GetUnderlyingType(from) is not null)
                return Expression.Condition(
                    Expression.Property(value, nameof(Nullable<int>.HasValue)),
                    ToText(Expression.Property(value, nameof(Nullable<int>.Value))),
                    Expression.Constant(null, typeof(string)));
            if (!from.IsValueType)
                return null;
            if (from == typeof(DateTime))
                return Expression.Call(Method(nameof(FormatDateTime)), value);
            if (from == typeof(DateTimeOffset))
                return Expression.Call(Method(nameof(FormatDateTimeOffset)), value);
            if (typeof(IFormattable).IsAssignableFrom(from))
                return Expression.Call(Method(nameof(FormatFormattable)).MakeGenericMethod(from), value);
            return Expression.Call(value, from.GetMethod(nameof(ToString), Type.EmptyTypes));
        }

        private static bool IsNumeric(Type type) => Type.GetTypeCode(type) switch
        {
            TypeCode.SByte or TypeCode.Byte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or
            TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or
            TypeCode.Decimal => !type.IsEnum,
            _ => false,
        };

        private static MethodInfo Method(string name) =>
            typeof(ParquetValueConversion).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);

        /// <summary>INT96 and timestamp columns carry no zone; their values are UTC.</summary>
        internal static DateTime AsUtc(DateTime value) => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

        internal static DateTimeOffset ToUtcOffset(DateTime value) => new(AsUtc(value));

        private static TEnum ParseEnum<TEnum>(string text) where TEnum : struct, Enum =>
            Enum.Parse<TEnum>(text.Trim(), ignoreCase: true);

        private static DateTime ParseDateTime(string text) =>
            AsUtc(DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

        private static DateTimeOffset ParseDateTimeOffset(string text) =>
            DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

        private static bool ParseBoolean(string text) => text.Trim() switch
        {
            "1" => true,
            "0" => false,
            var other => bool.Parse(other),
        };

        private static T ParseParsable<T>(string text) where T : IParsable<T> =>
            T.Parse(text.Trim(), CultureInfo.InvariantCulture);

        private static string FormatDateTime(DateTime value) => value.ToString("o", CultureInfo.InvariantCulture);

        private static string FormatDateTimeOffset(DateTimeOffset value) => value.ToString("o", CultureInfo.InvariantCulture);

        private static string FormatFormattable<T>(T value) where T : IFormattable =>
            value.ToString(null, CultureInfo.InvariantCulture);
    }
}
