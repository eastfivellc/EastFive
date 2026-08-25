using EastFive.Api.Bindings;
using EastFive.Api.Meta.OpenApi;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using EastFive.Serialization;

namespace EastFive.Api
{
    [PropertyJsonBinder]
    [PropertyFileBinder]
    [PropertyStringBinder]
    public struct Property<T>
    {
        public Property(T value)
        {
            specified = true;
            this.value = value;
        }

        public bool specified;
        public T value;
    }

    public class PropertyJsonBinderAttribute : Attribute,
            IBindParameter<JToken>, IBindApiParameter<JToken>
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

        public TResult Bind<TResult>(Type type, JToken content,
                IApplication application,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure)
        {
            BindTypeDelegate<int,int> d = PropertyJsonBinderAttribute.BindType<int,int>;

            var innerType = type.GetGenericArguments().First();
            return (TResult) //typeof(PropertyJsonBinderAttribute)
                             //.GetMethod("BindType", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                d.Method.GetGenericMethodDefinition()
                .MakeGenericMethod(new Type[] { innerType, typeof(TResult) })
                .Invoke(null, new object[] { type, content, onParsed, onDidNotBind, onBindingFailure });
        }

        private delegate TResult BindTypeDelegate<T, TResult>(Type type, JToken content,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure);

        public static TResult BindType<T, TResult>(Type type, JToken content, 
            Func<object, TResult> onParsed, 
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure)
        {
            var innerType = typeof(T);
            return StandardJTokenBindingsAttribute.BindDirect(innerType, content,
                innerValue =>
                {
                    var prop = new Property<T>
                    {
                        specified = true,
                        value = (T)innerValue,
                    };
                    return onParsed(prop);
                },
                onDidNotBind:(why) =>
                {
                    return onDidNotBind(why);
                },
                onBindingFailure);
        }

        public TResult Bind<TResult>(Type type, JsonReader content,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure)
        {
            throw new NotImplementedException();
        }
    }

    public class PropertyFileBinderAttribute : Attribute,
            IParsePropertyFormCollection
    {

        public TResult ParsePropertyFromFormCollection<TResult>(string key, IFormCollection formData,
                ParameterInfo parameterInfo, IApplication httpApp,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            ParseContentDelegateDelegate<int, int> d = PropertyFileBinderAttribute.ParseContentDelegateType<int, int>;

            var innerType = parameterInfo.ParameterType.GetGenericArguments().First();
            return (TResult) //typeof(PropertyJsonBinderAttribute)
                             //.GetMethod("BindType", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                d.Method.GetGenericMethodDefinition()
                .MakeGenericMethod(new Type[] { innerType, typeof(TResult) })
                .Invoke(null, new object[] { formData, key, parameterInfo, onParsed, onFailure});

        }

        private delegate TResult ParseContentDelegateDelegate<T, TResult>(IFormCollection formData,
                string key,
                ParameterInfo parameterInfo,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure);

        public static TResult ParseContentDelegateType<T, TResult>(IFormCollection formData,
                string key,
                ParameterInfo parameterInfo,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            foreach (var formItem in formData)
            {
                if (formItem.Key == key)
                {

                    return StandardStringBindingsAttribute.BindDirect(
                            typeof(T), formItem.Value,
                        onParsed: (innerValue) =>
                        {
                            var prop = new Property<T>
                            {
                                specified = true,
                                value = (T)innerValue,
                            };
                            return onParsed(prop);
                        },
                        onDidNotBind:(why) => onFailure(why),
                        onBindingFailure: (why) => onFailure(why));
                }
            }

            var unspecifiedProp = new Property<T>
            {
                specified = false,
                value = (T)typeof(T).GetDefault(),
            };
            return onParsed(unspecifiedProp);
        }
    }

    public class PropertyStringBinderAttribute : Attribute,
            IBindParameter<string>, IBindApiParameter<string>
    {
        public TResult Bind<TResult>(ParameterInfo parameter, string content,
                IApplication application,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure) => Bind(parameter.ParameterType, content,
                application: application,
                onParsed: onParsed,
                onDidNotBind: onDidNotBind,
                onBindingFailure: onBindingFailure);

        public TResult Bind<TResult>(Type type, string content,
                IApplication application,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure)
        {
            BindTypeDelegate<int, int> d = PropertyStringBinderAttribute.BindType<int, int>;

            var innerType = type.GetGenericArguments().First();
            return (TResult)
                d.Method.GetGenericMethodDefinition()
                .MakeGenericMethod(new Type[] { innerType, typeof(TResult) })
                .Invoke(null, new object[] { type, content, onParsed, onDidNotBind, onBindingFailure });
        }

        private delegate TResult BindTypeDelegate<T, TResult>(Type type, string content,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure);

        public static TResult BindType<T, TResult>(Type type, string content,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure)
        {
            var innerType = typeof(T);
            return StandardStringBindingsAttribute.BindDirect(innerType, content,
                innerValue =>
                {
                    var prop = new Property<T>
                    {
                        specified = true,
                        value = (T)innerValue,
                    };
                    return onParsed(prop);
                },
                onDidNotBind,
                onBindingFailure);
        }

    }
}
