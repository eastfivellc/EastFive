using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Api.Serialization;
using EastFive.Reflection;
using EastFive.Api.Bindings;

namespace EastFive.Api
{
    public interface IDeserializeFromBody<TBodyProvider>
    {
        object UpdateInstance(string propertyKey, TBodyProvider reader,
            object instance,
            ParameterInfo parameterInfo, MemberInfo memberInfo,
            IApplication httpApp, IHttpRequest request);
    }

    /// <summary>
    /// Generalization of body-only deserialization to any per-source value
    /// reader. <typeparamref name="TSource"/> identifies where the value
    /// is coming from (e.g. <c>JsonReader</c> for a JSON body, <c>string</c>
    /// for query/path/form scalars). A single attribute (e.g.
    /// <c>[ApiProperty]</c>) may implement this interface for several
    /// source types so the same metadata drives binding from any of them.
    /// </summary>
    public interface IDeserializeFromRequest<TSource>
        : IDeserializeFromBody<TSource>
    {
        bool IsMatch(string propertyKey,
            ParameterInfo parameterInfo, MemberInfo member,
            IApplication httpApp, IHttpRequest request);
    }

    /// <summary>
    /// Body-only flavor kept for back-compat. Equivalent to
    /// <see cref="IDeserializeFromRequest{TSource}"/> with a body-shaped
    /// source. New code should prefer <c>IDeserializeFromRequest&lt;…&gt;</c>.
    /// </summary>
    public interface IDeserializeFromApiBody<TBodyProvider>
        : IDeserializeFromRequest<TBodyProvider>
    {
    }

    public class DeserializeBodyAttribute : System.Attribute, 
        IBindApiValue, IBindJsonApiValue, IBindMultipartApiValue, IBindFormDataApiValue
    {
        public string GetKey(ParameterInfo paramInfo)
        {
            return default;
        }

        public SelectParameterResult TryCast(BindingData bindingData)
        {
            var parameterRequiringValidation = bindingData.parameterRequiringValidation;
            return bindingData.fetchBodyParam(parameterRequiringValidation,
                (value) => SelectParameterResult.Body(value, string.Empty, parameterRequiringValidation),
                (why) => SelectParameterResult.FailureBody(why, string.Empty, parameterRequiringValidation));
        }

        public TResult ParseContentDelegate<TResult>(JContainer contentJObject,
                string contentString, BindConvert bindConvert, ParameterInfo parameterInfo, 
                IApplication httpApp, IHttpRequest request,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            try
            {
                var parameterType = parameterInfo.ParameterType;
                var typeMembers = parameterType
                    .GetPropertyAndFieldsWithAttributesInterface<IDeserializeFromApiBody<JsonReader>>()
                    .ToArray();
                var instance = Activator.CreateInstance(parameterType);
                using (var reader = contentJObject.CreateReader())
                {
                    while(reader.Read())
                    {
                        if(reader.TokenType == JsonToken.PropertyName)
                        {
                            var propertyKey = (string)reader.Value;
                            if (!reader.Read())
                                continue;
                            instance = typeMembers
                                .Where(tm => tm.Item2.IsMatch(propertyKey, parameterInfo, tm.Item1,
                                    httpApp, request))
                                .Aggregate(instance,
                                    (instance, tpl) =>
                                    {
                                        var (memberInfo, deserializer) = tpl;
                                        return deserializer.UpdateInstance(
                                            propertyKey, reader,
                                            instance,
                                            parameterInfo, memberInfo, httpApp, request);
                                    });
                        }
                    }
                    return onParsed(instance);
                }
            }
            catch (Exception ex)
            {
                return onFailure(ex.Message);
            }
        }

        public TResult ParseContentDelegate<TResult>(
                IDictionary<string, MultipartContentTokenParser> content, 
                ParameterInfo parameterInfo, 
                IApplication httpApp, IHttpRequest request, 
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            var paramType = parameterInfo.ParameterType;
            var obj = paramType
                .GetPropertyAndFieldsWithAttributesInterface<IProvideApiValue>(true)
                .Aggregate(Activator.CreateInstance(paramType),
                    (param, memberProvideApiValueTpl) =>
                    {
                        var (member, provideApiValue) = memberProvideApiValueTpl;

                        var propertyName = provideApiValue.GetPropertyName(member);
                        if (!content.ContainsKey(propertyName))
                            return param;
                        
                        var tokenParser = content[propertyName];
                        return ContentToType(httpApp, member.GetMemberType(),
                            tokenParser,
                            paramValue =>
                            {
                                member.SetValue(ref param, paramValue);
                                return param;
                            },
                            why => param);
                    });
            return onParsed(obj);
        }

        internal static TResult ContentToType<TResult>(IApplication httpApp, Type type,
                MultipartContentTokenParser tokenReader,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
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
            return httpApp.Bind(strValue, type,
                (value) =>
                {
                    return onParsed(value);
                },
                why => onFailure(why));
        }

        public TResult ParseContentDelegate<TResult>(IFormCollection formData,
                ParameterInfo parameterInfo, 
                IApplication httpApp, IHttpRequest request,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            var paramType = parameterInfo.ParameterType;
            var obj = paramType
                .GetPropertyAndFieldsWithAttributesInterface<IProvideApiValue>(true)
                .Aggregate(Activator.CreateInstance(paramType),
                    (param, memberProvideApiValueTpl) =>
                    {
                        var (member, provideApiValue) = memberProvideApiValueTpl;

                        return ParseFormContentDelegate(provideApiValue.GetPropertyName(member), formData,
                                member.GetMemberType(), httpApp,
                            paramValue =>
                            {
                                member.SetValue(ref param, paramValue);
                                return param;
                            },
                            why =>
                            {
                                return httpApp.Bind(default(string), member.GetMemberType(),
                                    defaultValue =>
                                    {
                                        member.SetValue(ref param, defaultValue);
                                        return param;
                                    },
                                    why => param);
                            });
                    });
            return onParsed(obj);
        }

        public static TResult ParseFormContentDelegate<TResult>(string key, IFormCollection formData,
                Type type, IApplication httpApp,
            Func<object, TResult> onParsed,
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
                        return httpApp.Bind(strValue, type,
                            (value) =>
                            {
                                return onParsed(value);
                            },
                            why => onFailure(why));
                    },
                    () =>
                    {
                        return formData.Files
                            .Where(file => file.Name == key)
                            .First(
                                (file, next) =>
                                {
                                    var strValue = (IFormFile)file;
                                    return httpApp.Bind(strValue, type,
                                        (value) =>
                                        {
                                            return onParsed(value);
                                        },
                                        why => onFailure(why));
                                },
                                () => onFailure("Key not found"));
                    });

        }
    }
}
