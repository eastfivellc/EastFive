using System;
using System.Globalization;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using EastFive.Api.Serialization.Binding.Sinks;
using EastFive.Serialization.Binding;

namespace EastFive.Api.Serialization.Binding.Binders
{
    /// <summary>
    /// <see cref="JToken"/>, <see cref="JObject"/>, <see cref="JArray"/> and
    /// <see cref="JValue"/> members pass through verbatim, deep-cloned both ways so
    /// the written JSON and the read value never share nodes with the caller's.
    /// A member typed narrower than the token it meets (a <see cref="JObject"/>
    /// reading an array) fails with <see cref="WrongSourceType"/>.
    /// </summary>
    public sealed class JsonTokenBinder : ITypeBinder
    {
        public bool CanBind(Type targetType) => typeof(JToken).IsAssignableFrom(targetType);

        public ValueTask<TResult> Read<TResult>(
            Type targetType,
            IBindingSource source,
            IBindingContext context,
            Func<object, TResult> onBound,
            Func<BindFailure, TResult> onFailure,
            Func<TResult> onNull = null)
        {
            var path = context?.KeyPath ?? string.Empty;
            if (!JsonTokens.TryGet(source, path, targetType, out var token, out var failure))
                return new ValueTask<TResult>(onFailure(failure));
            if (JsonTokens.IsNull(token))
                return new ValueTask<TResult>(onNull is not null ? onNull() : onBound(null));
            if (!targetType.IsInstanceOfType(token))
                return new ValueTask<TResult>(onFailure(JsonTokens.WrongShape(Kind(targetType), token, targetType, path)));
            return new ValueTask<TResult>(onBound(token.DeepClone()));
        }

        public void Write(Type sourceType, object value, IBindingSink sink, IBindingContext context)
        {
            if (value is null) { sink.WriteNull(); return; }
            if (sink is not JTokenBindingSink json)
                throw new InvalidOperationException(
                    $"A {sourceType.Name} member can only be written to a JSON sink, not {sink.GetType().Name}.");
            json.WriteToken((JToken)value);
        }

        private static string Kind(Type type) =>
            typeof(JObject).IsAssignableFrom(type) ? "object"
            : typeof(JArray).IsAssignableFrom(type) ? "array"
            : typeof(JValue).IsAssignableFrom(type) ? "value"
            : "token";
    }

    /// <summary>
    /// <see cref="string"/> from any JSON scalar, as the text it stands for: a
    /// date token (what <c>JObject.Parse</c> makes of a string that merely looks
    /// like a date) reads as the ISO 8601 text Newtonsoft writes for it, a number
    /// or bool as its JSON text (<c>1.50</c>, <c>true</c>). Objects and arrays fail.
    /// </summary>
    public sealed class JsonStringBinder : ITypeBinder
    {
        public bool CanBind(Type targetType) => targetType == typeof(string);

        public ValueTask<TResult> Read<TResult>(
            Type targetType,
            IBindingSource source,
            IBindingContext context,
            Func<object, TResult> onBound,
            Func<BindFailure, TResult> onFailure,
            Func<TResult> onNull = null)
        {
            var path = context?.KeyPath ?? string.Empty;
            if (!JsonTokens.TryGet(source, path, targetType, out var token, out var failure))
                return new ValueTask<TResult>(onFailure(failure));
            if (JsonTokens.IsNull(token))
                return new ValueTask<TResult>(onNull is not null
                    ? onNull()
                    : onFailure(new BindFailure(new NullValue(), targetType, path)));
            var text = JsonTokens.ScalarText(token);
            if (text is null)
                return new ValueTask<TResult>(onFailure(JsonTokens.WrongShape("string", token, targetType, path)));
            return new ValueTask<TResult>(onBound(text));
        }

        public void Write(Type sourceType, object value, IBindingSink sink, IBindingContext context)
        {
            if (value is null) { sink.WriteNull(); return; }
            sink.WriteString((string)value);
        }
    }

    /// <summary>
    /// <see cref="Uri"/> written as its <see cref="Uri.OriginalString"/>, as
    /// Newtonsoft writes it (the core binder writes the canonicalized,
    /// partly-unescaped <see cref="Uri.ToString"/>), and read from a string or URI
    /// token as an absolute or relative URI.
    /// </summary>
    public sealed class JsonUriBinder : ITypeBinder
    {
        public bool CanBind(Type targetType) => targetType == typeof(Uri);

        public ValueTask<TResult> Read<TResult>(
            Type targetType,
            IBindingSource source,
            IBindingContext context,
            Func<object, TResult> onBound,
            Func<BindFailure, TResult> onFailure,
            Func<TResult> onNull = null)
        {
            var path = context?.KeyPath ?? string.Empty;
            if (!JsonTokens.TryGet(source, path, targetType, out var token, out var failure))
                return new ValueTask<TResult>(onFailure(failure));
            if (JsonTokens.IsNull(token))
                return new ValueTask<TResult>(onNull is not null
                    ? onNull()
                    : onFailure(new BindFailure(new NullValue(), targetType, path)));
            if (token is JValue { Value: Uri uri })
                return new ValueTask<TResult>(onBound(uri));
            if (token.Type == JTokenType.String && Uri.TryCreate((string)token, UriKind.RelativeOrAbsolute, out var parsed))
                return new ValueTask<TResult>(onBound(parsed));
            return new ValueTask<TResult>(onFailure(token.Type == JTokenType.String
                ? new BindFailure(new ParseError($"'{(string)token}' is not a Uri"), targetType, path)
                : JsonTokens.WrongShape("string", token, targetType, path)));
        }

        public void Write(Type sourceType, object value, IBindingSink sink, IBindingContext context)
        {
            if (value is null) { sink.WriteNull(); return; }
            sink.WriteString(((Uri)value).OriginalString);
        }
    }

    /// <summary>
    /// <see cref="decimal"/>, <see cref="float"/> and <see cref="ulong"/> written
    /// as the exact JSON number Newtonsoft writes (not widened through
    /// <see cref="double"/> / narrowed through <see cref="long"/>), and read from a
    /// JSON number or a numeric string.
    /// </summary>
    public sealed class JsonNumberBinder : ITypeBinder
    {
        public bool CanBind(Type targetType) =>
            targetType == typeof(decimal) || targetType == typeof(float) || targetType == typeof(ulong);

        public ValueTask<TResult> Read<TResult>(
            Type targetType,
            IBindingSource source,
            IBindingContext context,
            Func<object, TResult> onBound,
            Func<BindFailure, TResult> onFailure,
            Func<TResult> onNull = null)
        {
            var path = context?.KeyPath ?? string.Empty;
            if (!JsonTokens.TryGet(source, path, targetType, out var token, out var failure))
                return new ValueTask<TResult>(onFailure(failure));
            if (JsonTokens.IsNull(token))
                return new ValueTask<TResult>(onNull is not null
                    ? onNull()
                    : onFailure(new BindFailure(new NullValue(), targetType, path)));

            if (token is not JValue value ||
                (value.Type != JTokenType.Integer && value.Type != JTokenType.Float && value.Type != JTokenType.String))
                return new ValueTask<TResult>(onFailure(JsonTokens.WrongShape("number", token, targetType, path)));

            try
            {
                return new ValueTask<TResult>(onBound(value.Type == JTokenType.String
                    ? Parse(targetType, (string)value.Value)
                    : Convert(targetType, value)));
            }
            catch (Exception ex) when (ex is FormatException || ex is OverflowException || ex is InvalidCastException)
            {
                return new ValueTask<TResult>(onFailure(new BindFailure(
                    new ParseError($"'{value}' is not a {targetType.Name}"), targetType, path)));
            }
        }

        private static object Convert(Type type, JValue value)
        {
            // Newtonsoft parses integers beyond long as BigInteger, which Value<T> can't narrow.
            if (value.Value is System.Numerics.BigInteger big)
            {
                if (type == typeof(decimal)) return (decimal)big;
                if (type == typeof(float)) return (float)big;
                return (ulong)big;
            }
            if (type == typeof(decimal)) return value.Value<decimal>();
            if (type == typeof(float)) return value.Value<float>();
            return value.Value<ulong>();
        }

        private static object Parse(Type type, string text)
        {
            if (type == typeof(decimal)) return decimal.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
            if (type == typeof(float)) return float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
            return ulong.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        public void Write(Type sourceType, object value, IBindingSink sink, IBindingContext context)
        {
            if (value is null) { sink.WriteNull(); return; }
            if (sink is JTokenBindingSink json)
            {
                json.WriteToken(value switch
                {
                    decimal d => new JValue(d),
                    ulong u => new JValue(u),
                    _ => new JValue(value),
                });
                return;
            }
            switch (value)
            {
                case decimal d: sink.WriteDouble((double)d); return;
                case float f: sink.WriteDouble(f); return;
                case ulong u when u <= long.MaxValue: sink.WriteInt64((long)u); return;
                default: sink.WriteString(System.Convert.ToString(value, CultureInfo.InvariantCulture)); return;
            }
        }
    }

    /// <summary>
    /// <see cref="DateTimeOffset"/> written with its offset (the core binder
    /// writes the UTC instant through <see cref="IBindingSink.WriteDateTime"/>),
    /// and read from a date token, an ISO 8601 string, or ticks.
    /// </summary>
    public sealed class JsonDateTimeOffsetBinder : ITypeBinder
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
            var path = context?.KeyPath ?? string.Empty;
            if (!JsonTokens.TryGet(source, path, targetType, out var token, out var failure))
                return new ValueTask<TResult>(onFailure(failure));
            if (JsonTokens.IsNull(token))
                return new ValueTask<TResult>(onNull is not null
                    ? onNull()
                    : onFailure(new BindFailure(new NullValue(), targetType, path)));

            if (token is JValue value)
            {
                switch (value.Value)
                {
                    case DateTimeOffset dto:
                        return new ValueTask<TResult>(onBound(dto));
                    case DateTime dt:
                        return new ValueTask<TResult>(onBound(new DateTimeOffset(dt)));
                    case long ticks:
                        return new ValueTask<TResult>(onBound(new DateTimeOffset(new DateTime(ticks), TimeSpan.Zero)));
                    case string s:
                        if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                            return new ValueTask<TResult>(onBound(parsed));
                        return new ValueTask<TResult>(onFailure(new BindFailure(
                            new ParseError($"'{s}' is not a DateTimeOffset"), targetType, path)));
                }
            }
            return new ValueTask<TResult>(onFailure(JsonTokens.WrongShape("datetime", token, targetType, path)));
        }

        public void Write(Type sourceType, object value, IBindingSink sink, IBindingContext context)
        {
            if (value is null) { sink.WriteNull(); return; }
            var dto = (DateTimeOffset)value;
            if (sink is JTokenBindingSink json)
            {
                json.WriteToken(new JValue(dto));
                return;
            }
            sink.WriteDateTime(dto.UtcDateTime);
        }
    }
}
