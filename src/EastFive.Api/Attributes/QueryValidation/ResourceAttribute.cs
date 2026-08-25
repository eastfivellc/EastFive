using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

using Newtonsoft.Json.Linq;

using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Api.Serialization;
using EastFive.Reflection;
using EastFive.Api.Bindings;

namespace EastFive.Api
{
    public class ResourceAttribute : System.Attribute, 
        IBindApiValue, IBindJsonApiValue, IBindMultipartApiValue, IBindFormDataApiValue, IBindTextApiValue,
        IProvideBindingRequirements
    {
        public string GetKey(ParameterInfo paramInfo)
        {
            return default;
        }

        public (IReadOnlyList<BindingRequirement> requirements, AssembleParameter assemble)
            GetParameterBinding(ParameterInfo parameter)
            => (new[] { GetRequirement(parameter) }, values => (values[0], null));

        public BindingRequirement GetRequirement(ParameterInfo parameter)
        {
            return new BindingRequirement(
                    path: string.Empty,
                    source: BindingSource.Body,
                    parameter: parameter,
                    isOptional: false)
                .AddConverter<JContainer>((raw, param, httpApp, request, onParsed, onFailure) =>
                {
                    var contentString = raw?.ToString(Newtonsoft.Json.Formatting.None);
                    var bindConvert = new BindConvert(request, httpApp as HttpApplication);
                    return this.ParseContentDelegate<BindResult>(raw, contentString,
                        bindConvert: bindConvert, param, httpApp, request, onParsed, onFailure);
                })
                .AddConverter<IFormCollection>((raw, param, app, req, onParsed, onFailure) =>
                    this.ParseContentDelegate<BindResult>(raw, param, app, req, onParsed, onFailure))
                .AddConverter<string>((raw, param, app, req, onParsed, onFailure) =>
                    this.ParseContentDelegate<BindResult>(raw, param, app, req, onParsed, onFailure));
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
                if(!IsObjectNotArray(contentString))
                    return onFailure("Content is not a valid JSON object or array.");
                
                var rootObject = Newtonsoft.Json.JsonConvert.DeserializeObject(
                    contentString, parameterInfo.ParameterType, bindConvert);
                return onParsed(rootObject);

                bool IsObjectNotArray(string content)
                {
                    foreach (var ch in content)
                    {
                        if (ch == '{')
                            return true;
                        if (ch == '[')
                            return false;
                    }
                    return false;
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
                        return ContentToType(httpApp, member.GetMemberType(), parameterInfo,
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

        internal static TResult ContentToType<TResult>(IApplication httpApp,
                Type type, ParameterInfo parameterInfo,
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
            return httpApp.Bind(strValue, parameterInfo,
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
                        if (!member.IsSettable())
                            return param;

                        return ParseFormContentDelegate(provideApiValue.GetPropertyName(member), formData,
                                member.GetMemberType(), parameterInfo, httpApp,
                            paramValue =>
                            {
                                member.SetValue(ref param, paramValue);
                                return param;
                            },
                            why =>
                            {
                                return httpApp.Bind<string, object>(default(string), member,
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
                MemberInfo member, ParameterInfo parameter, IApplication httpApp,
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
                        return httpApp.Bind(strValue, parameter,
                            (value) =>
                            {
                                return onParsed(value);
                            },
                            why =>
                            {
                                return httpApp.Bind(strValue, member,
                                    (value) =>
                                    {
                                        return onParsed(value);
                                    },
                                    why => onFailure(why));
                            });
                    },
                    () =>
                    {
                        return formData.Files
                            .Where(file => file.Name == key)
                            .First(
                                (fileValue, next) =>
                                {
                                    return httpApp.Bind(fileValue, member,
                                        (value) =>
                                        {
                                            return onParsed(value);
                                        },
                                        why =>
                                        {
                                            return httpApp.Bind(fileValue, member.GetType(),
                                                (value) =>
                                                {
                                                    return onParsed(value);
                                                },
                                                why => onFailure(why));
                                        });
                                },
                                () => onFailure("Key not found"));
                    });

        }

        public TResult ParseContentDelegate<TResult>(string rawContent,
                ParameterInfo parameterInfo,
                IApplication httpApp, IHttpRequest request,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            if (parameterInfo.ParameterType.IsAssignableFrom(typeof(string)))
                return onParsed(rawContent);

            return onFailure($"Cannot bind raw string to {parameterInfo.ParameterType}");
        }
    }
}
