using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using EastFive;
using EastFive.Extensions;
using EastFive.Reflection;
using EastFive.Linq.Expressions;
using EastFive.Linq;
using EastFive.Api.Bindings;
using Newtonsoft.Json.Linq;
using EastFive.Api.Serialization;
using Microsoft.AspNetCore.Http;

namespace EastFive.Api
{
    [MutateResource]
    public delegate T MutateResource<T>(T resource);

    public class MutateResourceAttribute 
        : Attribute, IBindApiValue, IBindJsonApiValue, IBindMultipartApiValue, IBindFormDataApiValue
    {
        public static MutateResource<T> BuildMutator<T>(
            Func<string, Type, (object, bool)> getPropertyValue)
        {
            var resourceType = typeof(T);
            Func<T, T> inop = t => t;

            return (MutateResource<T>)resourceType
                // .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .GetMembers()
                .TryWhere(
                    (MemberInfo prop, out IProvideApiValue apiValueProvider) =>
                        prop.TryGetAttributeInterface(out apiValueProvider))
                .Aggregate(inop,
                    (mutator, propertyApiProvider) =>
                    {
                        var (property, apiValueProvider) = propertyApiProvider;

                        var bindingToType = typeof(Property<>)
                            .MakeGenericType(property.GetPropertyOrFieldType());
                        var (boundPropertyValue, success) = getPropertyValue(
                            apiValueProvider.GetPropertyName(property), bindingToType);
                        if (!success)
                            return mutator;
                        var isSpecified = (bool)bindingToType
                            .GetField(nameof(Property<int>.specified))
                            .GetValue(boundPropertyValue);
                        if(!isSpecified)
                            return mutator;
                        var boundValue = bindingToType
                            .GetField(nameof(Property<int>.value))
                            .GetValue(boundPropertyValue);
                        Func<T, T> operate =
                            resourceToMutate =>
                            {
                                var resourceMutatedByPrevious = mutator(resourceToMutate);
                                property.SetValue(ref resourceMutatedByPrevious, boundValue);
                                return resourceMutatedByPrevious;
                            };
                        return operate;
                        //return BindingExtensions.Bind(httpApp, provider,
                        //        parameterInfo,
                        //    boundValue =>
                        //    {
                        //        Func<T, T> operate = 
                        //            resourceToMutate =>
                        //            {
                        //                var resourceMutatedByPrevious = mutator(resourceToMutate);
                        //                property.SetValue(resourceToMutate, boundValue);
                        //                return resourceToMutate;
                        //            };
                        //        return operate;
                        //    },
                        //    why => mutator);
                        //var bindMethod = typeof(BindingExtensions)
                        //    .GetMethods(BindingFlags.Static | BindingFlags.Public)
                        //    .Where(method => method.Name == "Bind")
                        //    .Where(method => method.GetParameters()[2].ParameterType == typeof(ParameterInfo))
                        //    .First()
                        //    .MakeGenericMethod(bindingToType);
                        //bindMethod.Invoke(null, new object[] {httpApp, } )
                        //httpApp.Bind(property.property)

                        //return inop;
                    })
                .MakeDelegate(typeof(MutateResource<T>));

        }

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

        public TResult ParseContentDelegate<TResult>(
                Func<string, Type, (object, bool)> getPropertyValue,
                ParameterInfo parameterInfo,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            try
            {
                var bindMethod = typeof(MutateResourceAttribute)
                    .GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .Where(method => method.Name == nameof(BuildMutator))
                    .First()
                    .MakeGenericMethod(parameterInfo.ParameterType.GenericTypeArguments.First());
                var mutateResource = bindMethod.Invoke(null, new object[] { getPropertyValue, });

                return onParsed(mutateResource);
            }
            catch (Exception ex)
            {
                return onFailure(ex.Message);
            }
        }

        public TResult ParseContentDelegate<TResult>(JContainer contentJContainer,
                string contentString, BindConvert bindConvert, ParameterInfo parameterInfo,
                IApplication httpApp, IHttpRequest request,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            if (!(contentJContainer is JObject))
                return onFailure($"JSON Content is {contentJContainer.Type} and mutation can only be performed from objects.");
            var contentJObject = contentJContainer as JObject;

            return ParseContentDelegate(GetPropertyValue, parameterInfo,
                onParsed,
                onFailure);

            (object, bool)  GetPropertyValue(string key, Type parameterType)
            {
                if (!contentJObject.TryGetValue(key, out JToken valueToken))
                    return ($"Key[{key}] was not found in JSON", false);

                var parameterValueType = parameterType.IsSubClassOfGeneric(typeof(Property<>))
                    ? parameterType.GetGenericArguments().First()
                    : parameterType;

                try
                {
                    //var tokenParser = new Serialization.JsonTokenParser(valueToken);
                    return httpApp.Bind(
                            valueToken, parameterValueType,
                        obj =>
                        {
                            if(parameterValueType != parameterType)
                            {
                                var propertyInstance = Activator.CreateInstance(parameterType, obj);
                                return (propertyInstance, true);
                            }
                            return (obj, true);
                        },
                        (why) =>
                        {
                            // TODO: Get BindConvert to StandardJTokenBindingAttribute
                            if (valueToken.Type == JTokenType.Object || valueToken.Type == JTokenType.Array)
                            {
                                if (parameterType.IsSubClassOfGeneric(typeof(Property<>)))
                                {
                                    try
                                    {
                                        var value = Newtonsoft.Json.JsonConvert.DeserializeObject(
                                            valueToken.ToString(),
                                            parameterType.GetGenericArguments().First(),
                                            bindConvert);
                                        var propertyValue = Activator.CreateInstance(parameterType, value);
                                        return (propertyValue, true);
                                    }
                                    catch (Newtonsoft.Json.JsonSerializationException jsEx)
                                    {
                                        return (jsEx.Message, false);
                                    }
                                }

                                try
                                {
                                    var value = Newtonsoft.Json.JsonConvert.DeserializeObject(
                                        valueToken.ToString(), parameterType, bindConvert);
                                    return (value, true);
                                }
                                catch (Newtonsoft.Json.JsonSerializationException jsEx)
                                {
                                    return (jsEx.Message, false);
                                }
                            }
                            return (why, false);
                        });
                }
                catch (Exception ex)
                {
                    return (ex.Message, false);
                }
            }
        }

        public TResult ParseContentDelegate<TResult>(
                IDictionary<string, MultipartContentTokenParser> contentsLookup,
                ParameterInfo parameterInfo,
                IApplication httpApp, IHttpRequest request,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            Func<string, Type, (object, bool)> getPropertyValue =
                (key, propertyType) =>
                {
                    if (!contentsLookup.ContainsKey(key))
                        return (null, false);
                    return PropertyAttribute.ContentToType(
                            httpApp, propertyType,
                            contentsLookup[key],
                        (obj) => (obj, true),
                        strValue =>
                        {
                            return httpApp.Bind(strValue, propertyType,
                                (value) =>
                                {
                                    return (value, true);
                                },
                                why => (why, true));
                        });
                };
            return ParseContentDelegate(getPropertyValue, parameterInfo,
                onParsed,
                onFailure);
        }

        public TResult ParseContentDelegate<TResult>(
                IFormCollection formData,
                ParameterInfo parameterInfo,
                IApplication httpApp, IHttpRequest request,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            if (formData.IsDefaultOrNull())
                return onFailure("No form data provided");
            Func<string, Type, (object, bool)> getPropertyValue =
                    (key, propertyType) =>
                    {
                        return PropertyAttribute.ParseContentDelegate(
                                key, formData,
                            (strValue) =>
                            {
                                return httpApp.Bind(strValue, propertyType,
                                        (value) =>
                                        {
                                            return (value, true);
                                        },
                                        why => (why, false));
                            },
                            formFile =>
                            {
                                return httpApp.Bind(formFile, propertyType,
                                    (value) =>
                                    {
                                        return (value, true);
                                    },
                                    why => (why, false));
                            },
                            onFailure: (why) => (why, false));
                    };
            return ParseContentDelegate(getPropertyValue, parameterInfo,
                onParsed,
                onFailure);
        }
    }
}
