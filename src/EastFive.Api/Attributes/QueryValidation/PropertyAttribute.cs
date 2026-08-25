using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

using Microsoft.AspNetCore.Http;

using Newtonsoft.Json.Linq;

using EastFive;
using EastFive.Reflection;
using EastFive.Api.Bindings;
using EastFive.Api.Resources;
using EastFive.Extensions;
using EastFive.Linq;


namespace EastFive.Api
{
    public class PropertyAttribute : QueryValidationAttribute,
        IDocumentParameter, IBindJsonApiValue, IBindMultipartApiValue, IBindFormDataApiValue,
        IProvideBindingRequirements
    {
        public override SelectParameterResult TryCast(BindingData bindingData)
        {
            var request = bindingData.request;
            var parameterRequiringValidation = bindingData.parameterRequiringValidation;
            var name = this.GetKey(parameterRequiringValidation);
            return bindingData.fetchBodyParam(parameterRequiringValidation,
                vCasted => SelectParameterResult.Body(vCasted, name, parameterRequiringValidation),
                why => SelectParameterResult.FailureBody(why, name, parameterRequiringValidation));
        }

        public virtual (IReadOnlyList<BindingRequirement> requirements, AssembleParameter assemble)
            GetParameterBinding(ParameterInfo parameter)
            => (new[] { GetRequirement(parameter) }, values => (values[0], null));

        public virtual BindingRequirement GetRequirement(ParameterInfo parameter)
        {
            return new BindingRequirement(
                    path: this.GetKey(parameter),
                    source: BindingSource.Body,
                    parameter: parameter,
                    isOptional: false)
                .AddConverter<JContainer>((raw, param, app, req, onParsed, onFailure) =>
                    this.ParseContentDelegate<BindResult>(raw, contentString: null, bindConvert: null,
                        param, app, req, onParsed, onFailure))
                .AddConverter<IFormCollection>((raw, param, app, req, onParsed, onFailure) =>
                    this.ParseContentDelegate<BindResult>(raw, param, app, req, onParsed, onFailure));
        }

        public virtual TResult Convert<TResult>(HttpApplication httpApp, Type type, object value,
            Func<object, TResult> onCasted,
            Func<string, TResult> onInvalid)
        {
            if (value.IsDefaultOrNull())
            {
                return onCasted(type.GetDefault());
            }

            if (type.IsAssignableFrom(value.GetType()))
                return onCasted(value);

            if (value is Guid?)
            {
                var guidMaybe = value as Guid?;
                if (typeof(Guid).GUID == type.GUID)
                {
                    if (!guidMaybe.HasValue)
                        return onInvalid("Value did not provide a UUID.");
                    return onCasted(guidMaybe.Value);
                }
                if (typeof(Guid?).GUID == type.GUID)
                {
                    return onCasted(guidMaybe);
                }
            }

            if (value is string)
            {
                var valueString = value as string;
                if (typeof(Guid).GUID == type.GUID)
                {
                    if (Guid.TryParse(valueString, out Guid valueGuid))
                        return onCasted(valueGuid);
                    return onInvalid($"[{valueString}] is not a valid UUID.");
                }
                if (typeof(Guid?).GUID == type.GUID)
                {
                    if (valueString.IsNullOrWhiteSpace())
                        return onCasted(default(Guid?));
                    if (Guid.TryParse(valueString, out Guid valueGuid))
                        return onCasted(valueGuid);
                    return onInvalid($"[{valueString}] needs to be empty or a valid UUID.");
                }
                if (typeof(DateTime).GUID == type.GUID)
                {
                    if (DateTime.TryParse(valueString, out DateTime valueDateTime))
                        return onCasted(valueDateTime);
                    return onInvalid($"[{valueString}] needs to be a valid date/time.");
                }
                if (typeof(DateTime?).GUID == type.GUID)
                {
                    if (valueString.IsNullOrWhiteSpace())
                        return onCasted(default(DateTime?));
                    if (DateTime.TryParse(valueString, out DateTime valueDateTime))
                        return onCasted(valueDateTime);
                    return onInvalid($"[{valueString}] needs to be empty or a valid date/time.");
                }

                if (type.IsEnum)
                {
                    if (Enum.IsDefined(type, valueString))
                    {
                        var valueEnum = Enum.Parse(type, valueString);
                        return onCasted(valueEnum);
                    }
                    return onInvalid($"{valueString} is not one of [{Enum.GetNames(type).Join(",")}]");
                }

                if (typeof(Type).GUID == type.GUID)
                {
                    return httpApp.GetResourceType(valueString,
                            (typeInstance) => onCasted(typeInstance),
                            () => valueString.GetClrType(
                                typeInstance => onCasted(typeInstance),
                                () => onInvalid(
                                    $"`{valueString}` is not a recognizable resource type or CLR type.")));
                }
            }

            if (value is int)
            {
                if (type.IsEnum)
                {
                    var valueInt = (int)value;
                    var valueEnum = Enum.ToObject(type, valueInt);
                    return onCasted(valueEnum);
                }
            }

            if (value.GetType().IsArray)
            {
                if (type.IsArray)
                {
                    var elementType = type.GetElementType();
                    var array = (object[])value;

                    //var casted = Array.ConvertAll(array,
                    //    item => item.ToString());
                    //var typeConverted = casted.Cast<int>().ToArray();

                    var casted = Array
                        .ConvertAll(array,
                            item => Convert(httpApp, elementType, item, (v) => v, (why) => elementType.GetDefault()))
                        .CastArray(elementType);
                    //var typeConvertedEnumerable = typeof(System.Linq.Enumerable)
                    //    .GetMethod("Cast", BindingFlags.Static | BindingFlags.Public)
                    //    .MakeGenericMethod(new Type[] { elementType })
                    //    .Invoke(null, new object[] { casted });
                    //var typeConvertedArray = typeof(System.Linq.Enumerable)
                    //    .GetMethod("ToArray", BindingFlags.Static | BindingFlags.Public)
                    //    .MakeGenericMethod(new Type[] { elementType })
                    //    .Invoke(null, new object[] { typeConvertedEnumerable });

                    return onCasted(casted);
                }
            }

            return onInvalid($"Could not convert `{value.GetType().FullName}` to `{type.FullName}`.");
        }

        public virtual Parameter GetParameter(ParameterInfo paramInfo, HttpApplication httpApp)
        {
            return new Parameter(paramInfo)
            {
                Default = false,
                Name = this.GetKey(paramInfo),
                Required = true,
                Type = Parameter.GetTypeName(paramInfo.ParameterType, httpApp),
                Where = "BODY",
                OpenApiType = Parameter.GetOpenApiTypeName(paramInfo.ParameterType, httpApp),
            };
        }

        public TResult ParseContentDelegate<TResult>(JContainer contentJContainer,
                string contentString, Serialization.BindConvert bindConvert,
                ParameterInfo paramInfo,
                IApplication httpApp, IHttpRequest request,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            if (contentJContainer is JObject)
            {
                var contentJObject = contentJContainer as JObject;

                var key = this.GetKey(paramInfo);
                return ParseJsonContentDelegate(contentJObject,
                        contentString, bindConvert,
                        key, paramInfo,
                        httpApp, request,
                    onParsed,
                    onFailure);
            }

            if(contentJContainer is JArray)
            {
                var contentJArray = contentJContainer as JArray;
                var paramType = paramInfo.ParameterType;
                var key = this.GetKey(paramInfo);
                if (paramType.IsArray)
                {
                    var elementType = paramType.GetElementType();
                    var arrayOfType = contentJArray
                        .Select(
                            contentJToken =>
                            {
                                if (contentJToken is JObject)
                                {
                                    var contentJObject = (JObject)contentJToken;
                                    if (!contentJObject.TryGetValue(key, out JToken valueToken))
                                        return elementType.GetDefault();
                                    return httpApp.Bind(valueToken, elementType,
                                        obj => obj,
                                        why => elementType.GetDefault());
                                }
                                return elementType.GetDefault();
                            })
                        .CastArray(elementType);
                    return onParsed(arrayOfType);
                }
            }

            return onFailure($"JSON Content is {contentJContainer.Type} and non-array properties can only be parsed from objects.");
        }

        public static TResult ParseJsonContentDelegate<TResult>(JObject contentJObject,
                string contentString, Serialization.BindConvert bindConvert,
                string key, ParameterInfo paramInfo,
                IApplication httpApp, IHttpRequest request,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            if (!contentJObject.TryGetValue(key, out JToken valueToken))
                return onFailure($"Key[{key}] was not found in JSON");

            try
            {
                //var tokenParser = new Serialization.JsonTokenParser(valueToken);
                return httpApp.Bind(valueToken, paramInfo,
                    obj => onParsed(obj),
                    (why) =>
                    {
                        // TODO: Get BindConvert to StandardJTokenBindingAttribute
                        if (valueToken.Type == JTokenType.Object || valueToken.Type == JTokenType.Array)
                        {
                            try
                            {
                                var tokenValueString = valueToken.ToString();
                                var value = Newtonsoft.Json.JsonConvert.DeserializeObject(
                                    tokenValueString, paramInfo.ParameterType, bindConvert);
                                return onParsed(value);
                            }
                            catch (Newtonsoft.Json.JsonSerializationException)
                            {
                                throw;
                            }
                        }
                        return onFailure(why);
                    });
            }
            catch (Exception ex)
            {
                return onFailure(ex.Message);
            }
        }

        public TResult ParseContentDelegate<TResult>(
                IDictionary<string, MultipartContentTokenParser> contentsLookup,
                ParameterInfo parameterInfo, IApplication httpApp, IHttpRequest request,
            Func<object, TResult> onParsed, 
            Func<string, TResult> onFailure)
        {
            var key = this.GetKey(parameterInfo);
            var type = parameterInfo.ParameterType;
            if (!contentsLookup.ContainsKey(key))
                return onFailure("Key not found");

            return ContentToType(httpApp, parameterInfo, contentsLookup[key],
                    onParsed,
                    onFailure);
        }

        internal static TResult ContentToType<TResult>(IApplication httpApp, ParameterInfo paramInfo,
            MultipartContentTokenParser tokenReader,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            var type = paramInfo.ParameterType;
            return ContentToType(httpApp, type, tokenReader,
                onParsed,
                strValue =>
                {
                    return httpApp.Bind(strValue, paramInfo,
                        (value) =>
                        {
                            return onParsed(value);
                        },
                        why => onFailure(why));
                });
        }

        internal static TResult ContentToType<TResult>(IApplication httpApp, Type type,
                MultipartContentTokenParser tokenReader,
            Func<object, TResult> onParsed,
            Func<string, TResult> onNeedsStringParsed)
        {
            if (type.IsAssignableFrom(typeof(Stream)))
            {
                var streamValue = tokenReader.ReadStream();
                return onParsed((object)streamValue);
            }
            if (type.IsAssignableFrom(typeof(byte[])))
            {
                var byteArrayValue = tokenReader.ReadBytes();
                return onParsed((object)byteArrayValue);
            }
            if (type.IsAssignableFrom(typeof(HttpContent)))
            {
                var content = tokenReader.ReadObject<HttpContent>();
                return onParsed((object)content);
            }
            if (type.IsAssignableFrom(typeof(System.Net.Http.Headers.ContentDispositionHeaderValue)))
            {
                var content = tokenReader.ReadObject<HttpContent>();
                var header = content.Headers.ContentDisposition;
                return onParsed((object)header);
            }
            if (type.IsAssignableFrom(typeof(ByteArrayContent)))
            {
                var content = tokenReader.ReadObject<ByteArrayContent>();
                return onParsed((object)content);
            }
            var strValue = tokenReader.ReadString();
            return onNeedsStringParsed(strValue);
        }

        public virtual TResult ParseContentDelegate<TResult>(IFormCollection formData, 
                ParameterInfo parameterInfo, IApplication httpApp, IHttpRequest routeData,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            var key = this.GetKey(parameterInfo);
            return ParseContentDelegate<TResult>(key, formData,
                    parameterInfo, httpApp,
                onParsed,
                onFailure);
        }

        public static TResult ParseContentDelegate<TResult>(string key, IFormCollection formData,
                ParameterInfo parameterInfo, IApplication httpApp,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            if(parameterInfo.ParameterType.TryGetAttributeInterface<IParsePropertyFormCollection>(out var propContentParser))
            {
                return propContentParser.ParsePropertyFromFormCollection(key, formData,
                        parameterInfo, httpApp,
                    onParsed,
                    onFailure);
            }

            return ParseContentDelegate(key, formData,
                (strValue) =>
                {
                    return httpApp.Bind(strValue, parameterInfo,
                            (value) =>
                            {
                                return onParsed(value);
                            },
                            why => onFailure(why));
                },
                formFile =>
                {
                    return httpApp.Bind(formFile, parameterInfo,
                        (value) =>
                        {
                            return onParsed(value);
                        },
                        why => onFailure(why));
                },
                onFailure: onFailure);
        }

        public static TResult ParseContentDelegate<TResult>(string key, IFormCollection formData,
            Func<string, TResult> onStringNeedsBinding,
            Func<IFormFile, TResult> onFileNeedsBinding,
            Func<string, TResult> onFailure)
        {
            if (formData.IsDefaultOrNull())
                return onFailure("No form data provided");

            return formData
                .Where(kvp => kvp.Key == key)
                .First(
                    (kvp, next) =>
                    {
                        var strValue = (string)kvp.Value;
                        return onStringNeedsBinding(strValue);
                    },
                    () =>
                    {
                        return formData.Files
                            .Where(file => file.Name == key)
                            .First(
                                (file, next) =>
                                {
                                    var strValue = (IFormFile)file;
                                    return onFileNeedsBinding(strValue);
                                },
                                () => onFailure("Key not found"));
                    });

        }
    }

    public interface IParsePropertyFormCollection
    {
        TResult ParsePropertyFromFormCollection<TResult>(string key, IFormCollection formData,
                ParameterInfo parameterInfo, IApplication httpApp,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure);
    }
}
