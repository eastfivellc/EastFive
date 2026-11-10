using System;
using System.Globalization;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using EastFive.Api.Serialization.Binding.Sources;
using EastFive.Serialization.Binding;

namespace EastFive.Api.Serialization.Binding.Binders
{
    /// <summary>
    /// Shared plumbing for the JSON-specific binders <see cref="JsonBinding"/>
    /// layers over <see cref="TypeBindings.Default"/>.
    /// </summary>
    internal static class JsonTokens
    {
        /// <summary>
        /// The raw token at <paramref name="path"/>. Over a
        /// <see cref="JTokenBindingSource"/> this is the token itself; any other
        /// source (e.g. the string elements of a delimited-list fallback) is
        /// rebuilt as a scalar <see cref="JValue"/>. Composite shapes on a
        /// non-JSON source fail with <see cref="WrongSourceType"/>.
        /// </summary>
        public static bool TryGet(IBindingSource source, string path, Type expected, out JToken token, out BindFailure failure)
        {
            if (source is JTokenBindingSource json)
            {
                if (json.TryGetToken(path, out token, out failure))
                    return true;
                failure = new BindFailure(failure.Reason, expected, failure.KeyPath);
                return false;
            }

            JToken rebuilt = null;
            BindFailure? rebuildFailure = null;
            Complete(source.GetValue<bool>(
                path: path,
                onNull: () => { rebuilt = JValue.CreateNull(); return true; },
                onString: s => { rebuilt = new JValue(s); return true; },
                onGuid: g => { rebuilt = new JValue(g); return true; },
                onBool: b => { rebuilt = new JValue(b); return true; },
                onInt64: i => { rebuilt = new JValue(i); return true; },
                onDouble: d => { rebuilt = new JValue(d); return true; },
                onDateTime: dt => { rebuilt = new JValue(dt); return true; },
                onBytes: b => { rebuilt = new JValue(b); return true; },
                onFailure: f => { rebuildFailure = f; return false; }));
            token = rebuilt;
            failure = rebuildFailure is { } rf ? new BindFailure(rf.Reason, expected, rf.KeyPath) : default;
            return rebuildFailure is null;
        }

        public static bool IsNull(JToken token) =>
            token is null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined;

        public static BindFailure WrongShape(string expectedKind, JToken token, Type expected, string path) =>
            new BindFailure(new WrongSourceType(expectedKind, token?.Type.ToString() ?? "nothing"), expected, path ?? string.Empty);

        /// <summary>
        /// The text a scalar token stands for: a string as-is; a date as the ISO
        /// 8601 text Newtonsoft writes for it (so a string that <c>JObject.Parse</c>
        /// sniffed into a date reads back as the canonical text it came from); any
        /// other scalar as its JSON text. Null for objects and arrays.
        /// </summary>
        public static string ScalarText(JToken token)
        {
            if (token is not JValue value) return null;
            switch (value.Type)
            {
                case JTokenType.String:
                    return (string)value.Value;
                case JTokenType.Date:
                    return Unquote(value.Value is DateTimeOffset dto ? JsonConvert.ToString(dto) : JsonConvert.ToString((DateTime)value.Value));
                case JTokenType.Guid:
                    return ((Guid)value.Value).ToString("D");
                case JTokenType.Uri:
                    return ((Uri)value.Value).OriginalString;
                case JTokenType.TimeSpan:
                    return ((TimeSpan)value.Value).ToString("c", CultureInfo.InvariantCulture);
                case JTokenType.Bytes:
                    return Convert.ToBase64String((byte[])value.Value);
                case JTokenType.Integer:
                case JTokenType.Float:
                case JTokenType.Boolean:
                    return value.ToString(Formatting.None);
                default:
                    return null;
            }
        }

        private static string Unquote(string quoted) => quoted.Substring(1, quoted.Length - 2);

        /// <summary>Binds complete synchronously over JSON sources; block only as a fallback.</summary>
        public static T Complete<T>(ValueTask<T> task) =>
            task.IsCompletedSuccessfully ? task.Result : task.AsTask().GetAwaiter().GetResult();

        /// <summary>Bind <paramref name="type"/> from a token through the context's bindings, as a new root.</summary>
        public static (bool ok, object value, BindFailure failure) BindToken(Type type, JToken token, IBindingContext context)
        {
            object bound = null;
            BindFailure failure = default;
            var ok = Complete(context.TypeBindings.Bind<bool>(
                type,
                new JTokenBindingSource(token, literalKeys: true),
                context.WithKeyPath(string.Empty),
                v => { bound = v; return true; },
                f => { failure = f; return false; },
                onNull: () => { bound = null; return true; }));
            return (ok, bound, failure);
        }

        /// <summary>Prefix a nested failure's path with <paramref name="segment"/> (a key or an <c>[index]</c>).</summary>
        public static BindFailure Under(string segment, BindFailure inner)
        {
            var path = string.IsNullOrEmpty(inner.KeyPath)
                ? segment
                : inner.KeyPath[0] == '[' ? segment + inner.KeyPath : $"{segment}.{inner.KeyPath}";
            return new BindFailure(inner.Reason, inner.ExpectedType, path);
        }

        public static object DefaultOf(Type type) =>
            type.IsValueType && Nullable.GetUnderlyingType(type) is null ? Activator.CreateInstance(type) : null;
    }
}
