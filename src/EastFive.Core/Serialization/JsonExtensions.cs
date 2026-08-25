using System;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using EastFive;
using EastFive.Extensions;
using EastFive.Collections.Generic;
using System.Linq.Expressions;
using System.Diagnostics;

namespace EastFive.Serialization.Json
{
    public static class JsonExtensions
    {
        public static TResult JsonParseObject<TResult>(this string jsonData, Type type,
            Func<object, TResult> onSuccess,
            Func<string, TResult> onFailureToParse = default,
            Func<Exception, TResult> onException = default,
                JsonConverter[] converters = default)
        {
            try
            {
                if (type == typeof(string))
                    return onSuccess(jsonData);

                if (jsonData.IsNull())
                    return onFailureToParse("Null data");

                var resource = JsonConvert.DeserializeObject(jsonData, type, converters: converters);
                return onSuccess(resource);
            }
            catch (JsonReaderException jsonEx)
            {
                if (onFailureToParse.IsNotDefaultOrNull())
                    return onFailureToParse(jsonEx.Message);

                throw;
            }
            catch (JsonSerializationException jsonEx)
            {
                if (onFailureToParse.IsNotDefaultOrNull())
                    return onFailureToParse(jsonEx.Message);

                throw;
            }
            catch (Exception ex)
            {
                if (onException.IsNotDefaultOrNull())
                    return onException(ex);

                if (onFailureToParse.IsNotDefaultOrNull())
                    return onFailureToParse(ex.Message);

                throw;
            }
        }

        public static TResult JsonParse<TResource, TResult>(this string jsonData,
            Func<TResource, TResult> onSuccess,
            Func<string, TResult> onFailureToParse = default,
            Func<Exception, TResult> onException = default,
                JsonConverter[] converters = default)
        {
            TResource resource = default;
            try
            {
                if (typeof(TResource) == typeof(string))
                    return onSuccess((TResource)(object)jsonData);

                if (jsonData.IsNull())
                    return onFailureToParse("Null data");

                resource = JsonConvert.DeserializeObject<TResource>(jsonData, converters: converters);
            }
            catch (JsonReaderException jsonEx)
            {
                if (onFailureToParse.IsNotDefaultOrNull())
                    return onFailureToParse(jsonEx.Message);

                throw;
            }
            catch (JsonSerializationException jsonEx)
            {
                if (onFailureToParse.IsNotDefaultOrNull())
                    return onFailureToParse(jsonEx.Message);

                throw;
            }
            catch(Exception ex)
            {
                if (onException.IsNotDefaultOrNull())
                    return onException(ex);
                
                if (onFailureToParse.IsNotDefaultOrNull())
                    return onFailureToParse(ex.Message);

                throw;
            }
            return onSuccess(resource);
        }

        public static TResult JsonSerialize<TResource, TResult>(this TResource resource,
            Func<string, TResult> onSuccess,
            Func<string, TResult> onFailureToParse = default,
                JsonConverter[] converters = default)
        {
            try
            {
                var jsonData = JsonConvert.SerializeObject(resource, converters);
                return onSuccess(jsonData);
            }
            catch (JsonWriterException jsonEx)
            {
                if (onFailureToParse.IsNotDefaultOrNull())
                    return onFailureToParse(jsonEx.Message);

                throw new ArgumentException($"Failed to parse a `{typeof(TResource).FullName}` from the response.");
            }
        }

        public static IEnumerable<dynamic> AsEnumerableDynamics(this JArray array)
        {
            foreach (var item in array)
                yield return (dynamic)item;
        }

        public static IEnumerable<IDictionary<string, object>> AsEnumerableDictionary(this JArray array)
        {
            foreach (var item in array)
                yield return item.AsDictionary();
        }

        public static IDictionary<string, object> AsDictionary(this JToken item)
        {
            return item.Aggregate(
                (IDictionary<string, object>)new Dictionary<string, object>(),
                (valuesDictionary, token) =>
                {
                    var valueMaybe = token.AsKeyValuePair();
                    if (!valueMaybe.HasValue)
                        return valuesDictionary;
                    var value = valueMaybe.Value;
                    if (valuesDictionary.ContainsKey(value.Key))
                        return valuesDictionary;
                    return valuesDictionary.Append(value).ToDictionary();
                });
        }

        public static KeyValuePair<string, object>? AsKeyValuePair(this JToken token)
        {
            var key = token.Path.Split('.').Last();
            var jvalue = token.Values<object>().First();
            if (!(jvalue is Newtonsoft.Json.Linq.JValue))
                return default(KeyValuePair<string, object>?);
            var jValueValue = (jvalue as Newtonsoft.Json.Linq.JValue).Value;
            return key.PairWithValue(jValueValue);
        }
    }
}

