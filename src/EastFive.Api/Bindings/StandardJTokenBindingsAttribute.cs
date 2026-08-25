using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Net.Http;
using System.Net.Http.Headers;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using EastFive.Api.Resources;
using EastFive.Api.Serialization;
using EastFive.Reflection;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Linq;

namespace EastFive.Api.Bindings
{
    public class StandardJTokenBindingsAttribute : Attribute,
        IBindParameter<JToken>, IBindApiParameter<JToken>,
        IBindParameter<JsonReader>, IBindApiParameter<JsonReader>
    {
        public TResult Bind<TResult>(ParameterInfo parameter, JToken content,
                IApplication application,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure) => Bind(parameter.ParameterType, content,
                application: application,
                onParsed: onParsed,
                onDidNotBind: onDidNotBind,
                onBindingFailure: onBindingFailure);

        public TResult Bind<TResult>(ParameterInfo parameter, JsonReader content,
                IApplication application,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure) => Bind(parameter.ParameterType, content,
                application: application,
                onParsed: onParsed,
                onDidNotBind: onDidNotBind,
                onBindingFailure: onBindingFailure);

        public TResult Bind<TResult>(Type type, JToken content,
                IApplication application,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure)
        {
            return BindDirect(type, content,
                onParsed,
                onDidNotBind:(why) =>
                {
                    if (application is IApiApplication)
                    {
                        var apiApplication = application as IApiApplication;
                        if (type == typeof(Type))
                        {
                            if (content.Type == JTokenType.String)
                            {
                                var stringValue = content.Value<string>();
                                return apiApplication.GetResourceType(stringValue,
                                    type => onParsed(type),
                                    () => onDidNotBind($"Could not find type:{stringValue}"));
                            }
                        }
                    }
                    return onDidNotBind(why);
                }, 
                onBindingFailure);
        }

        public static TResult BindDirectWithApplication<TResult>(Type type, JToken content,
                IApplication application,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure)
        {
            return BindDirect(type, content,
                onParsed,
                onDidNotBind: (why) =>
                {
                    if (application is IApiApplication)
                    {
                        var apiApplication = application as IApiApplication;
                        if (type == typeof(Type))
                        {
                            if (content.Type == JTokenType.String)
                            {
                                var stringValue = content.Value<string>();
                                return apiApplication.GetResourceType(stringValue,
                                    type => onParsed(type),
                                    () => onDidNotBind($"Could not find type:{stringValue}"));
                            }
                        }
                    }
                    return onDidNotBind(why);
                },
                onBindingFailure);
        }

        public static TResult BindDirect<TResult>(Type type, JToken content,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure)
        {
            if (type == typeof(object))
            {
                var objectValue = ReadObject(content);
                return onParsed(objectValue);
            }
            if (type.IsAssignableFrom(typeof(Guid)))
            {
                if (content.Type == JTokenType.Guid)
                {
                    var guidValue = content.Value<Guid>();
                    return onParsed(guidValue);
                }
                if (content.Type == JTokenType.String)
                {
                    var stringValue = content.Value<string>();
                    if (Guid.TryParse(stringValue, out Guid guidValue))
                        return onParsed(guidValue);
                    return onBindingFailure($"Cannot convert `{stringValue}` to Guid.");
                }
            }

            if (type.IsSubClassOfGeneric(typeof(IRef<>)))
            {
                var activatableType = typeof(Ref<>).MakeGenericType(type.GenericTypeArguments);
                if (content.Type == JTokenType.Guid)
                {
                    var guidValue = content.Value<Guid>();
                    var iref = Activator.CreateInstance(activatableType, guidValue);
                    return onParsed(iref);
                }
                if (content.Type == JTokenType.String)
                {
                    return StandardStringBindingsAttribute.BindDirect(type,
                            content.Value<string>(),
                        onParsed,
                        onDidNotBind,
                        onBindingFailure);
                }
                if (content.Type == JTokenType.Object)
                {
                    var objectContent = (content as JObject);
                    if (objectContent.TryGetValue("uuid", StringComparison.OrdinalIgnoreCase, out JToken idContentUuid))
                        return BindDirect(type, idContentUuid, onParsed, onDidNotBind, onBindingFailure);
                    if (objectContent.TryGetValue("id", StringComparison.OrdinalIgnoreCase, out JToken idContentId))
                        return BindDirect(type, idContentId, onParsed, onDidNotBind, onBindingFailure);
                    var guidStr = objectContent.ToString();
                    return StandardStringBindingsAttribute.BindDirect(type,
                            guidStr,
                        onParsed,
                        onDidNotBind,
                        onBindingFailure);
                }
                if (content.Type == JTokenType.Null)
                    return onBindingFailure("Value was null.");
            }

            if (type.IsSubClassOfGeneric(typeof(IRefOptional<>)))
            {
                if (content.Type == JTokenType.Guid)
                {
                    var guidValue = content.Value<Guid>();
                    var activatableType = typeof(IRefOptional<>).MakeGenericType(type.GenericTypeArguments);
                    var iref = Activator.CreateInstance(activatableType, guidValue);
                    return onParsed(iref);
                }
                if (content.Type == JTokenType.Object)
                {
                    var objectContent = (content as JObject);
                    if (objectContent.TryGetValue("uuid", StringComparison.OrdinalIgnoreCase, out JToken idContent))
                        return BindDirect(type, idContent, onParsed, onDidNotBind, onBindingFailure);
                    var guidStr = objectContent.ToString();
                    return StandardStringBindingsAttribute.BindDirect(type,
                            guidStr,
                        onParsed,
                        onDidNotBind,
                        onBindingFailure);
                }
                if (content.Type == JTokenType.Null)
                {
                    var irefOptional = RefOptionalHelper.CreateEmpty(type.GenericTypeArguments.First());
                    return onParsed(irefOptional);
                }
                // Standard string binding fallback at end of function will work for this case
                // if (content.Type == JTokenType.String)
            }

            if (type.IsSubClassOfGeneric(typeof(IRefs<>)))
            {
                var activatableType = typeof(Refs<>).MakeGenericType(type.GenericTypeArguments);
                if (content.Type == JTokenType.Guid)
                {
                    var guidValue = content.Value<Guid>();
                    var guids = guidValue.AsArray();
                    var irefs = Activator.CreateInstance(activatableType, guids);
                    return onParsed(irefs);
                }
                if (content.Type == JTokenType.String)
                {
                    return StandardStringBindingsAttribute.BindDirect(type,
                            content.Value<string>(),
                        onParsed,
                        onDidNotBind,
                        onBindingFailure);
                }
                if (content.Type == JTokenType.Array)
                {
                    var guids = ReadArray(content)
                        .Select(
                            token => BindDirect(typeof(Guid), token,
                                guid => (Guid?)((Guid)guid),
                                (why) => default(Guid?),
                                (why) => default(Guid?)))
                        .SelectWhereHasValue()
                        .ToArray();

                    var irefs = Activator.CreateInstance(activatableType, guids);
                    return onParsed(irefs);
                }
            }

            if (type == typeof(int))
            {
                if (content.Type == JTokenType.Float)
                {
                    var floatValue = content.Value<double>();
                    var intValue = (int)floatValue;
                    return onParsed(intValue);
                }
                if (content.Type == JTokenType.Integer)
                {
                    var intValue = content.Value<int>();
                    return onParsed(intValue);
                }
                if (content.Type == JTokenType.String)
                {
                    var stringValue = content.Value<string>();
                    return StandardStringBindingsAttribute.BindDirect(type, stringValue,
                        onParsed,
                        onDidNotBind,
                        onBindingFailure);
                }
                return onDidNotBind($"Cannot convert `{content.Type}` to  {typeof(int).FullName}");
            }

            if (type == typeof(long))
            {
                if (content.Type == JTokenType.Float)
                {
                    var floatValue = content.Value<double>();
                    var longValue = (long)floatValue;
                    return onParsed(longValue);
                }
                if (content.Type == JTokenType.Integer)
                {
                    var longValue = content.Value<long>();
                    return onParsed(longValue);
                }
                if (content.Type == JTokenType.String)
                {
                    var stringValue = content.Value<string>();
                    return StandardStringBindingsAttribute.BindDirect(type, stringValue,
                        onParsed,
                        onDidNotBind,
                        onBindingFailure);
                }
                return onDidNotBind($"Cannot convert `{content.Type}` to  {typeof(long).FullName}");
            }

            if (type == typeof(double))
            {
                if (content.Type == JTokenType.Float)
                {
                    var floatValue = content.Value<double>();
                    return onParsed(floatValue);
                }
                if (content.Type == JTokenType.Integer)
                {
                    var intValue = content.Value<int>();
                    var floatValue = (double)intValue;
                    return onParsed(floatValue);
                }
                if (content.Type == JTokenType.String)
                {
                    var stringValue = content.Value<string>();
                    return StandardStringBindingsAttribute.BindDirect(type, stringValue,
                        onParsed,
                        onDidNotBind,
                        onBindingFailure);
                }
                return onDidNotBind($"Cannot convert `{content.Type}` to  {typeof(double).FullName}");
            }

            if (type == typeof(decimal))
            {
                if (content.Type == JTokenType.Float)
                {
                    var floatValue = content.Value<double>();
                    var decimalValule = (decimal)floatValue;
                    return onParsed(decimalValule);
                }
                if (content.Type == JTokenType.Integer)
                {
                    var intValue = content.Value<int>();
                    var floatValue = (decimal)intValue;
                    return onParsed(floatValue);
                }
                if (content.Type == JTokenType.String)
                {
                    var stringValue = content.Value<string>();
                    return StandardStringBindingsAttribute.BindDirect(type, stringValue,
                        onParsed,
                        onDidNotBind,
                        onBindingFailure);
                }
                return onDidNotBind($"Cannot convert `{content.Type}` to  {typeof(decimal).FullName}");
            }

            if (type == typeof(DateTime))
            {
                if (content.Type == JTokenType.Date)
                {
                    var dateValue = content.Value<DateTime>();
                    return onParsed(dateValue);
                }
                if (content.Type == JTokenType.Integer)
                {
                    var intValue = content.Value<long>();
                    var dateValue = new DateTime(intValue);
                    return onParsed(dateValue);
                }
                if (content.Type == JTokenType.String)
                {
                    var stringValue = content.Value<string>();
                    return StandardStringBindingsAttribute.BindDirect(type, stringValue,
                        onParsed,
                        onDidNotBind,
                        onBindingFailure);
                }
                return onDidNotBind($"Cannot convert `{content.Type}` to  {typeof(DateTime).FullName}");
            }

            if (type == typeof(bool))
            {
                if (content.Type == JTokenType.Boolean)
                {
                    var boolValue = content.Value<bool>();
                    return onParsed(boolValue);
                }
                if (content.Type == JTokenType.Integer)
                {
                    var intValue = content.Value<int>();
                    var boolValue = intValue != 0;
                    return onParsed(boolValue);
                }
                if (content.Type == JTokenType.String)
                {
                    var stringValue = content.Value<string>();
                    return StandardStringBindingsAttribute.BindDirect(type, stringValue,
                        onParsed,
                        onDidNotBind,
                        onBindingFailure);
                }
                return onDidNotBind($"Cannot convert `{content.Type}` to  {typeof(bool).FullName}");
            }

            if (type == typeof(Func<Task<byte[]>>))
            {
                if (content.Type == JTokenType.Bytes)
                {
                    var bytes = content.Value<byte[]>();
                    Func<Task<byte[]>> callback = () => bytes.AsTask();
                    return onParsed(callback);
                }
                if (content.Type == JTokenType.String)
                {
                    var stringValue = content.Value<string>();
                    if (stringValue.TryParseBase64String(out byte[] bytes))
                    {
                        Func<Task<byte[]>> callback = () => bytes.AsTask();
                        return onParsed(callback);
                    }
                    if (Uri.TryCreate(stringValue, UriKind.Absolute, out Uri url))
                    {
                        Func<Task<byte[]>> callback = async () =>
                        {
                            using (var client = new HttpClient())
                            {
                                using (var response = await client.GetAsync(url))
                                {
                                    var bytes = await response.Content.ReadAsByteArrayAsync();
                                    return bytes;
                                }
                            }
                        };
                        return onParsed(callback);
                    }
                    return GetHttpContentFromBase64(stringValue,
                        context =>
                        {
                            Func<Task<byte[]>> callback = context.ReadAsByteArrayAsync;
                            return onParsed(callback);
                        },
                        (prefix) => onBindingFailure($"Not a valid base64 string or a valid URL."));
                }
            }

            if (type == typeof(HttpContent) || type.IsSubclassOf(typeof(HttpContent)))
            {
                if (content.Type == JTokenType.String)
                {
                    var contentEncodedBase64String = content.Value<string>();
                    return contentEncodedBase64String.MatchRegexInvoke(
                        "data:(?<contentType>[^;]+);base64,(?<base64Data>.+)",
                        (contentType, base64Data) => base64Data.PairWithValue(contentType),
                        types => types.First(
                            (ct, next) =>
                            {
                                var base64EncodedData = ct.Key;
                                var data = base64EncodedData.FromBase64String();
                                var contentType = ct.Value;
                                var httpContent = new ByteArrayContent(data);
                                httpContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                                return onParsed(httpContent);
                            },
                            () => onBindingFailure(
                                $"Could not decode JSON Binary prefix:{contentEncodedBase64String.Substring(0, 25)}")));
                    
                }
                return onDidNotBind($"Cannot convert `{content.Type}` to  {typeof(HttpContent).FullName}");
            }

            if (type == typeof(Func<Task<HttpContent>>))
            {
                if (content.Type == JTokenType.String)
                {
                    var stringValue = content.Value<string>();
                    return GetHttpContentFromBase64(stringValue,
                        (content) =>
                        {
                            Func<Task<HttpContent>> func = () => content.AsTask();
                            return onParsed(content);
                        },
                        (prefix) =>
                        {
                            if (Uri.TryCreate(stringValue, UriKind.Absolute, out Uri url))
                            {
                                Func<Task<HttpContent>> callback = async () =>
                                {
                                    using (var client = new HttpClient())
                                    {
                                        using (var response = await client.GetAsync(url))
                                        {
                                            // Resource disposal creates issues so a copy is needed
                                            var data = await response.Content.ReadAsByteArrayAsync();
                                            var httpContent = new ByteArrayContent(data);
                                            foreach(var header in response.Content.Headers)
                                                httpContent.Headers.Add(header.Key, header.Value);
                                            return httpContent;
                                        }
                                    }
                                };
                                return onParsed(callback);
                            }
                            return onBindingFailure($"Not a valid base64 string or a valid URL.");
                        });
                }
                return onDidNotBind($"Cannot convert `{content.Type}` to  {typeof(Func<Task<HttpContent>>).FullName}");
            }

            TResult GetHttpContentFromBase64(string contentEncodedBase64String,
                Func<HttpContent, TResult> onSuccess,
                Func<string, TResult> onFailure)
            {
                return contentEncodedBase64String.MatchRegexInvoke(
                    "data:(?<contentType>[^;]+);base64,(?<base64Data>.+)",
                    (contentType, base64Data) => base64Data.PairWithValue(contentType),
                    types => types.First(
                        (ct, next) =>
                        {
                            var base64EncodedData = ct.Key;
                            var data = base64EncodedData.FromBase64String();
                            var contentType = ct.Value;
                            var httpContent = new ByteArrayContent(data);
                            httpContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                            return onSuccess(httpContent);
                        },
                        () => onFailure(contentEncodedBase64String.Substring(0, 25))));
            }

            if (type.IsNullable())
            {
                var nullableT = type.GetNullableUnderlyingType();
                return BindDirect(nullableT, content,
                    (v) =>
                    {
                        var nullableV = v.AsNullable();
                        return onParsed(nullableV);
                    },
                    (why) => onParsed(type.GetDefault()),
                    (why) => onParsed(type.GetDefault()));
            }

            if (type.IsAssignableFrom(typeof(IDictionary<,>)))
            {
                var keyType = type.GenericTypeArguments[0];
                var valueType = type.GenericTypeArguments[1];
                var refType = typeof(Dictionary<,>).MakeGenericType(type.GenericTypeArguments);
                var refInstance = Activator.CreateInstance(refType);
                var addMethod = refType.GetMethod("Add");
                //Dictionary<string, int> dict;
                //dict.Add()
                foreach (var kvpToken in ReadDictionary(content))
                {
                    var keyToken = kvpToken.Key;
                    var valueToken = kvpToken.Value;
                    string result = StandardStringBindingsAttribute.BindDirect(keyType, keyToken,
                        keyValue =>
                        {
                            return BindDirect(valueType, valueToken,
                                valueValue =>
                                {
                                    addMethod.Invoke(refInstance,
                                        new object[] { keyValue, valueValue });
                                    return string.Empty;
                                },
                                (why) => why,
                                (why) => why);
                        },
                        (why) => why,
                        (why) => why);
                }

                return onParsed(refInstance);
            }

            if (type.IsArray && type.GetElementType().IsAssignableFrom(typeof(KeyValuePair<,>)))
            {
                var elementType = type.GetElementType();
                var keyType = elementType.GenericTypeArguments[0];
                var valueType = elementType.GenericTypeArguments[1];
                var refType = typeof(Dictionary<,>).MakeGenericType(elementType.GenericTypeArguments);
                var refInstance = Activator.CreateInstance(refType);
                var addMethod = refType.GetMethod(nameof(Dictionary<int,int>.Add));
                //Dictionary<string, int> dict;
                //dict.Add()
                foreach (var kvpToken in ReadDictionary(content))
                {
                    var keyToken = kvpToken.Key;
                    var valueToken = kvpToken.Value;
                    string result = StandardStringBindingsAttribute.BindDirect(keyType, keyToken,
                        keyValue =>
                        {
                            return BindDirect(valueType, valueToken,
                                valueValue =>
                                {
                                    addMethod.Invoke(refInstance,
                                        new object[] { keyValue, valueValue });
                                    return string.Empty;
                                },
                                (why) => why,
                                (why) => why);
                        },
                        (why) => why,
                        (why) => why);
                }

                return onParsed(refInstance);
            }

            if (type == typeof(object))
            {
                var objectValue = ReadObject(content);
                return onParsed(objectValue);
            }

            if (content is JObject)
            {
                var jObj = content as JObject;
                var jsonText = jObj.ToString();
                var value = JsonConvert.DeserializeObject(jsonText, type,
                    new JsonSerializerSettings
                    {
                        ContractResolver = Serialization.Json.ApiPropertyContractResolver.Instance,
                    });
                return onParsed(value);
            }

            if (content.Type == JTokenType.String)
            {
                return StandardStringBindingsAttribute.BindDirect(type,
                        content.Value<string>(),
                    onParsed,
                    onDidNotBind,
                    onBindingFailure);
            }

            // if (content.Type == JTokenType.Object ||
            if(content.Type == JTokenType.Array)
            {
                if (type.IsArray && type.HasElementType)
                {
                    var elementType = type.GetElementType();
                    if (content.IsDefaultNullOrEmpty())
                    {
                        var value = Array.CreateInstance(elementType, 0);
                        return onParsed(value);
                    }
                    var arrayValue = content
                        .Children()
                        .Select(
                            child =>
                            {
                                try
                                {
                                    var value = BindDirect(elementType, child,
                                        x => x,
                                        why => elementType.GetDefault(),
                                        why => elementType.GetDefault());
                                    return value;
                                }
                                catch (Newtonsoft.Json.JsonSerializationException)
                                {
                                    return elementType.GetDefault();
                                }
                                catch(Exception)
                                {
                                    return elementType.GetDefault();
                                }
                            })
                        .CastArray(elementType);
                    return onParsed(arrayValue);
                }
            }

            if (content.Type == JTokenType.Null)
            {
                // PropertyAttribute will not recognize null values as specified w/o this.
                if(type.IsAssignableFrom(typeof(string)))
                    return onParsed((string)null);

                //var defaultValue = type.GetDefault();
                //return onParsed(defaultValue);
            }

            return onDidNotBind($"Could not find binding for type {type.FullName}");
        }


        public  static T ReadObject<T>(JToken valueToken)
        {
            if (valueToken is JObject)
            {
                var jObj = valueToken as Newtonsoft.Json.Linq.JObject;
                var jsonText = jObj.ToString();
                var value = JsonConvert.DeserializeObject<T>(jsonText);
                return value;
            }
            if (valueToken is JValue)
            {
                var jValue = valueToken as Newtonsoft.Json.Linq.JValue;
                var typeOfT = typeof(T).FullName;
                if (jValue.Type != JTokenType.Null)
                    if(!(jValue.Value is null))
                        if (typeof(T).IsAssignableFrom(jValue.Value.GetType()))
                            return (T)jValue.Value;
            }
            return valueToken.Value<T>();
        }

        public static object ReadObject(JToken token)
        {
            if (token is JValue)
            {
                if (token.Type == JTokenType.Boolean)
                    return token.Value<bool>();
                if (token.Type == JTokenType.Bytes)
                    return token.Value<byte[]>();
                if (token.Type == JTokenType.Date)
                    return token.Value<DateTime>();
                if (token.Type == JTokenType.Float)
                    return token.Value<float>();
                if (token.Type == JTokenType.Guid)
                    return token.Value<Guid>();
                if (token.Type == JTokenType.Integer)
                    return token.Value<int>();
                if (token.Type == JTokenType.None)
                    return null;
                if (token.Type == JTokenType.Null)
                    return null;
                if (token.Type == JTokenType.String)
                    return token.Value<string>();
                if (token.Type == JTokenType.Uri)
                    return token.Value<Uri>();
            }
            return token.ToObject<object>();
        }

        public static JToken[] ReadArray(JToken valueToken)
        {
            if (valueToken.Type == JTokenType.Array)
                return (valueToken as JArray)
                    .Select(
                        token => token)
                    .ToArray();
            if (valueToken.Type == JTokenType.Null)
                return new JToken[] { };
            if (valueToken.Type == JTokenType.Undefined)
                return new JToken[] { };

            if (valueToken.Type == JTokenType.Object)
                return valueToken
                    .Children()
                    .Select(childToken => childToken)
                    .ToArray();

            if (valueToken.Type == JTokenType.Property)
            {
                var property = (valueToken as JProperty);
                return property.Value.AsArray();
            }

            return valueToken
                .Children()
                .ToArray();
        }

        public static IDictionary<string, JToken> ReadDictionary(JToken valueToken)
        {
            KeyValuePair<string, JToken> ParseToken(JToken token)
            {
                if (token.Type == JTokenType.Property)
                {
                    var propertyToken = (token as JProperty);
                    return propertyToken.Value
                        .PairWithKey<string, JToken>(propertyToken.Name);
                }
                return token
                    .PairWithKey<string, JToken>(token.ToString());
            }

            if (valueToken.Type == JTokenType.Array)
                return (valueToken as JArray)
                    .Select(ParseToken)
                    .ToDictionary();
            if (valueToken.Type == JTokenType.Null)
                return new Dictionary<string, JToken>();
            if (valueToken.Type == JTokenType.Undefined)
                return new Dictionary<string, JToken>();

            if (valueToken.Type == JTokenType.Object)
                return valueToken
                    .Children()
                    .Select(ParseToken)
                    .ToDictionary();

            return valueToken
                    .Children()
                    .Select(ParseToken)
                    .ToDictionary();
        }

        public TResult Bind<TResult>(Type objectType, JsonReader reader,
                IApplication application,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure)
        {
            if (objectType.IsSubClassOfGeneric(typeof(IRef<>)))
            {
                return GetGuid(
                    (id) =>
                    {
                        var refType = typeof(Ref<>).MakeGenericType(objectType.GenericTypeArguments);
                        var value = Activator.CreateInstance(refType, id);
                        return onParsed(value);
                    },
                    onDidNotBind,
                    onBindingFailure);
            }

            if (objectType.IsSubClassOfGeneric(typeof(IRefOptional<>)))
            {
                return GetGuidMaybe(
                    id =>
                    {
                        if(!id.HasValue)
                        {
                            var emptyValue = RefOptionalHelper.CreateEmpty(objectType.GenericTypeArguments.First());
                            return onParsed(emptyValue);
                        }
                        var refType = typeof(RefOptional<>).MakeGenericType(objectType.GenericTypeArguments);
                        var value = Activator.CreateInstance(refType, id.Value);
                        return onParsed(value);
                    });
            }

            if (objectType.IsSubClassOfGeneric(typeof(IRefs<>)))
            {
                return GetGuids(
                    ids =>
                    {
                        var refType = typeof(Refs<>).MakeGenericType(objectType.GenericTypeArguments);
                        var value = Activator.CreateInstance(refType, ids);
                        return onParsed(value);
                    });
            }

            if (objectType.IsSubClassOfGeneric(typeof(IDictionary<,>)))
            {
                var dictionaryKeyType = objectType.GenericTypeArguments[0];
                var dictionaryValueType = objectType.GenericTypeArguments[1];
                var dictionaryType = typeof(Dictionary<,>).MakeGenericType(objectType.GenericTypeArguments);
                var instance = Activator.CreateInstance(dictionaryType);

                if (reader.TokenType != JsonToken.StartObject)
                    return onParsed(instance);
                if (!reader.Read())
                    return onParsed(instance);

                var addMethod = dictionaryType.GetMethod("Add", BindingFlags.Public | BindingFlags.Instance);
                do
                {
                    if (reader.TokenType == JsonToken.EndObject)
                        return onParsed(instance);
                    instance = StandardStringBindingsAttribute.BindDirect(dictionaryKeyType, reader.Path,
                        keyValue =>
                        {
                            var valueValue = Bind(dictionaryValueType, reader,
                                    application,
                                v => v,
                                why => dictionaryValueType.GetDefault(),
                                why => dictionaryValueType.GetDefault());
                            addMethod.Invoke(instance, new object[] { keyValue, valueValue });
                            return instance;
                        },
                        (why) => instance,
                        (why) => instance);
                    
                } while (reader.Read());
            }

            if (objectType == typeof(byte[]))
            {
                if (reader.TokenType == JsonToken.String)
                {
                    var bytesString = reader.Value as string;
                    var value = bytesString.FromBase64String();
                    return onParsed(value);
                }
            }

            if (objectType.IsAssignableFrom(typeof(Type)))
            {
                if (application is IApiApplication)
                {
                    var apiApplication = application as IApiApplication;
                    if (reader.TokenType == JsonToken.String)
                    {
                        var stringValue = reader.Value as string;
                        var (success, type) = apiApplication.GetResourceType(stringValue,
                            type => (true, type),
                            () => (false, default(Type)));
                        if (success)
                            return onParsed(type);
                    }
                }
            }

            if (objectType.IsNullable())
            {
                var underlyingType = objectType.GetNullableUnderlyingType();
                if (reader.TokenType == JsonToken.Null)
                    return onParsed(objectType.GetDefault());
                return Bind(underlyingType, reader,
                        application,
                    obj => onParsed(obj.AsNullable()),
                    (why) => onParsed(objectType.GetDefault()),
                    (why) => onParsed(objectType.GetDefault()));
            }

            // As a last ditch effort, see if the JToken deserialization will work.
            var token = JToken.ReadFrom(reader);
            return BindDirect(objectType, token,
                onParsed,
                onDidNotBind,
                onBindingFailure);


            TR GetGuid<TR>(
                Func<Guid, TR> onGot,
                Func<string, TR> onIgnored,
                Func<string, TR> onFailed)
            {
                if (reader.TokenType == JsonToken.String)
                {
                    var guidString = reader.Value as string;
                    if(Guid.TryParse(guidString, out Guid guid))
                        return onGot(guid);
                    return onFailed($"Cannot parse `{guidString}` as a Guid.");
                }
                if (reader.TokenType == JsonToken.StartObject)
                {
                    if (!reader.Read())
                        return onIgnored("Empty object");
                    return GetGuid(
                        guid =>
                        {
                            while (reader.TokenType != JsonToken.EndObject)
                            {
                                if (!reader.Read())
                                    break;
                            }
                            return onGot(guid);
                        },
                        onIgnored,
                        onFailed);
                }
                if (reader.TokenType == JsonToken.PropertyName)
                {
                    var propertyName = reader.Value as string;
                    if (!reader.Read())
                        return onFailed("Property did not have value.");
                    if (propertyName.ToLower() == "uuid")
                        return GetGuid(onGot, onIgnored, onFailed);
                    if (propertyName.ToLower() == "id")
                        return GetGuid(onGot, onIgnored, onFailed);
                    if (!reader.Read())
                        return onIgnored("'uuid' Property not found.");
                    return GetGuid(onGot, onIgnored, onFailed);
                }
                return onFailed($"Cannot decode token of type `{reader.TokenType}` to UUID.");
            }

            TResult GetGuidMaybe(Func<Guid?, TResult> callback)
            {
                if (reader.TokenType == JsonToken.Null)
                    return callback(default(Guid?));
                return GetGuid(
                    (x) => callback(x),
                    onDidNotBind,
                    onBindingFailure);
            }

            TResult GetGuids(Func<Guid[], TResult> onGot)
            {
                if (reader.TokenType == JsonToken.Null)
                    return onGot(new Guid[] { });

                if (reader.TokenType == JsonToken.StartArray)
                {
                    var list = new List<Guid>();
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonToken.EndArray)
                            break;

                        var result = GetGuid(
                            g => new
                            {
                                why = string.Empty,
                                g = g.AsOptional(),
                                success = true,
                            },
                            why => new
                            {
                                why = why,
                                g = default(Guid?),
                                success = true,
                            },
                            why => new
                            {
                                why = why,
                                g = default(Guid?),
                                success = false,
                            });

                        if (!result.success)
                            return onBindingFailure(result.why);
                        if (result.g.HasValue)
                            list.Add(result.g.Value);
                    }
                    return onGot(list.ToArray());
                }

                return onBindingFailure($"Cannot decode token of type `{reader.TokenType}` to {objectType.FullName}.");
            }
        }
    }
}
