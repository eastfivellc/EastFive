using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using EastFive.Serialization.Binding;
using EastFive.Serialization.Binding.Sources;

namespace EastFive.Api.Serialization.Binding.Sources
{
    /// <summary>
    /// <see cref="IBindingSource"/> over a Newtonsoft <see cref="JToken"/>. Navigates
    /// <c>path</c> (dotted + bracketed) into a child token, then dispatches on the
    /// token's <see cref="JTokenType"/>. No source-side coercion — a numeric token
    /// calls <c>onInt64</c> or <c>onDouble</c>, never <c>onString</c>.
    /// </summary>
    public sealed class JTokenBindingSource : IBindingSource, IKeyedBindingSource
    {
        private readonly JToken token;

        public JTokenBindingSource(JToken token)
        {
            this.token = token;
        }

        /// <summary>
        /// Property names at this source's root, when it wraps a JSON object.
        /// Empty for any other token shape. Backs <c>DictionaryBinder</c>, which
        /// pairs each key with a <c>GetValue(path: key, ...)</c> call on this
        /// same instance.
        /// </summary>
        public IEnumerable<string> Keys =>
            token is JObject obj ? obj.Properties().Select(p => p.Name) : Enumerable.Empty<string>();

        public ValueTask<TResult> GetValue<TResult>(
            string path = null,
            Func<TResult> onNull = null,
            Func<string, TResult> onString = null,
            Func<Guid, TResult> onGuid = null,
            Func<bool, TResult> onBool = null,
            Func<long, TResult> onInt64 = null,
            Func<double, TResult> onDouble = null,
            Func<DateTime, TResult> onDateTime = null,
            Func<byte[], TResult> onBytes = null,
            Func<IBindingSource, TResult> onObject = null,
            Func<IEnumerableBindingSource, TResult> onArray = null,
            Type elementTypeHint = null,
            Func<BindFailure, TResult> onFailure = null)
        {
            var (resolved, navFailure) = Navigate(token, path);
            if (navFailure is { } nf)
                return BindingSourceDispatch.FailTask(nf, onFailure);

            if (resolved is null ||
                resolved.Type == JTokenType.Null ||
                resolved.Type == JTokenType.Undefined)
                return BindingSourceDispatch.Null(typeof(object), path, onNull, onFailure);

            switch (resolved.Type)
            {
                case JTokenType.String:
                case JTokenType.Uri:
                    if (onString is not null)
                        return new ValueTask<TResult>(onString(resolved.Value<string>()));
                    return BindingSourceDispatch.WrongType<TResult>("string", resolved.Type.ToString(), typeof(object), path, onFailure);

                case JTokenType.Guid:
                    if (onGuid is not null)
                        return new ValueTask<TResult>(onGuid(resolved.Value<Guid>()));
                    return BindingSourceDispatch.WrongType<TResult>("guid", resolved.Type.ToString(), typeof(object), path, onFailure);

                case JTokenType.Integer:
                    if (onInt64 is not null)
                        return new ValueTask<TResult>(onInt64(resolved.Value<long>()));
                    return BindingSourceDispatch.WrongType<TResult>("int", resolved.Type.ToString(), typeof(object), path, onFailure);

                case JTokenType.Float:
                    if (onDouble is not null)
                        return new ValueTask<TResult>(onDouble(resolved.Value<double>()));
                    return BindingSourceDispatch.WrongType<TResult>("double", resolved.Type.ToString(), typeof(object), path, onFailure);

                case JTokenType.Boolean:
                    if (onBool is not null)
                        return new ValueTask<TResult>(onBool(resolved.Value<bool>()));
                    return BindingSourceDispatch.WrongType<TResult>("bool", resolved.Type.ToString(), typeof(object), path, onFailure);

                case JTokenType.Date:
                    if (onDateTime is not null)
                        return new ValueTask<TResult>(onDateTime(resolved.Value<DateTime>()));
                    return BindingSourceDispatch.WrongType<TResult>("datetime", resolved.Type.ToString(), typeof(object), path, onFailure);

                case JTokenType.Bytes:
                    if (onBytes is not null)
                        return new ValueTask<TResult>(onBytes(resolved.Value<byte[]>()));
                    return BindingSourceDispatch.WrongType<TResult>("bytes", resolved.Type.ToString(), typeof(object), path, onFailure);

                case JTokenType.Object:
                    if (onObject is not null)
                        return new ValueTask<TResult>(onObject(new JTokenBindingSource(resolved)));
                    return BindingSourceDispatch.WrongType<TResult>("object", resolved.Type.ToString(), typeof(object), path, onFailure);

                case JTokenType.Array:
                    if (onArray is not null)
                    {
                        var arr = (JArray)resolved;
                        var elements = arr.Select(t => (IBindingSource)new JTokenBindingSource(t));
                        var enumerable = new EnumerableBindingSource(elements, elementTypeHint);
                        return new ValueTask<TResult>(onArray(enumerable));
                    }
                    return BindingSourceDispatch.WrongType<TResult>("array", resolved.Type.ToString(), typeof(object), path, onFailure);

                default:
                    return BindingSourceDispatch.WrongType<TResult>("supported JTokenType", resolved.Type.ToString(), typeof(object), path, onFailure);
            }
        }

        private static (JToken resolved, BindFailure? failure) Navigate(JToken start, string path)
        {
            if (string.IsNullOrEmpty(path))
                return (start, null);
            var current = start;
            var remaining = path;
            while (!string.IsNullOrEmpty(remaining))
            {
                if (current is null)
                    return (null, new BindFailure(new NotPresent(), typeof(object), path));

                if (PathParser.TryConsumeIndex(remaining, out var index, out var afterIndex))
                {
                    if (current is JArray ja)
                    {
                        if (index < 0 || index >= ja.Count)
                            return (null, new BindFailure(new NotPresent(), typeof(object), path));
                        current = ja[index];
                        remaining = afterIndex;
                        continue;
                    }
                    return (null, new BindFailure(new WrongSourceType("array", current.Type.ToString()), typeof(object), path));
                }

                if (PathParser.TryConsumeBracketName(remaining, out var bname, out var afterBracket))
                {
                    if (current is JObject jb)
                    {
                        if (!TryGetMember(jb, bname, out var child))
                            return (null, new BindFailure(new NotPresent(), typeof(object), path));
                        current = child;
                        remaining = afterBracket;
                        continue;
                    }
                    return (null, new BindFailure(new WrongSourceType("object", current.Type.ToString()), typeof(object), path));
                }

                if (PathParser.TryConsumeName(remaining, out var name, out var afterName))
                {
                    if (current is JObject jo)
                    {
                        if (!TryGetMember(jo, name, out var child))
                            return (null, new BindFailure(new NotPresent(), typeof(object), path));
                        current = child;
                        remaining = afterName;
                        continue;
                    }
                    return (null, new BindFailure(new WrongSourceType("object", current.Type.ToString()), typeof(object), path));
                }

                return (null, new BindFailure(new ParseError($"Cannot parse path '{path}'"), typeof(object), path));
            }
            return (current, null);
        }

        private static bool TryGetMember(JObject jo, string key, out JToken value)
        {
            if (jo.TryGetValue(key, StringComparison.Ordinal, out value)) return true;
            if (jo.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out value)) return true;
            return false;
        }
    }
}
