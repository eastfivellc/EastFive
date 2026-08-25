using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

using EastFive.Extensions;
using System.Linq;
using System.Reflection;

namespace EastFive.Api.Bindings
{
    public class StandardFormDataBindingsAttribute :
        Attribute, IBindParameter<IFormFile>, IBindApiParameter<IFormFile>
    {
        public TResult Bind<TResult>(ParameterInfo parameter, IFormFile content,
                IApplication application,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure) => Bind(parameter.ParameterType, content,
                application: application,
                onParsed: onParsed,
                onDidNotBind: onDidNotBind,
                onBindingFailure: onBindingFailure);

        public TResult Bind<TResult>(Type type,
                IFormFile content,
                IApplication application,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure)
        {
            if (type.IsAssignableFrom(typeof(Stream)))
            {
                var streamValue = content.OpenReadStream();
                return onParsed((object)streamValue);
            }
            if (type.IsAssignableFrom(typeof(Func<Task<Stream>>)))
            {
                Func<Task<Stream>> callbackValue = () =>
                {
                    var streamValue = content.OpenReadStream();
                    return streamValue.AsTask();
                };
                return onParsed((object)callbackValue);
            }
            if (type.IsAssignableFrom(typeof(byte[])))
            {
                var stream = content.OpenReadStream();
                var bytes = new byte[content.Length];
                stream.ReadExactly(bytes);
                return onParsed((object)bytes);
            }
            if (type.IsAssignableFrom(typeof(Func<Task<byte[]>>)))
            {
                Func<Task<byte[]>> callbackValue = () =>
                {
                    var stream = content.OpenReadStream();
                    var bytes = new byte[content.Length];
                    stream.ReadExactly(bytes);
                    return bytes.AsTask();
                };
                return onParsed((object)callbackValue);
            }
            if (type.IsAssignableFrom(typeof(MediaTypeWithQualityHeaderValue)))
            {
                var header = new MediaTypeWithQualityHeaderValue(content.ContentType);
                return onParsed(header);
            }
            if (type.IsAssignableFrom(typeof(ContentDispositionHeaderValue)))
            {
                if(ContentDispositionHeaderValue.TryParse(content.ContentDisposition,
                        out ContentDispositionHeaderValue header))
                    return onParsed((object)header);
            }
            if (type.IsSubClassOfGeneric(typeof(ReadRequestBodyDelegateAsync<>)))
            {
                var data = new Data(content, application);
                var requestDelegate = InvokeHelper(data.BindFromByte<object>, type, data);
                return onParsed(requestDelegate);
            }
            var bytesGeneric = content.OpenReadStream().ToBytesAsync().Result;
            return application.Bind(bytesGeneric, type,
                (v) => onParsed(v),
                why => onDidNotBind(why));
            //return onDidNotBind(
            //    $"{type.FullName} is not supported from Form Data. Consider wrapping it as a ReadRequestBodyDelegateAsync<>");
        }

        private class Data
        {
            private IFormFile content;
            private IApplication application;

            public Data(IFormFile content, IApplication application)
            {
                this.content = content;
                this.application = application;
            }

            public async Task<T> BindFromByte<T>()
            {
                var bytes = await content.OpenReadStream().ToBytesAsync();
                var result = application.Bind(bytes, typeof(T),
                    (v) => (T)v,
                    why => throw new Exception(why));
                return result;
            }
        }

        static object InvokeHelper<T>(Func<Task<T>> actionWrongType, Type delegateType, object target)
        {
            var method = actionWrongType.Method;
            var genericMethod = method.GetGenericMethodDefinition();
            var concreteMethod = genericMethod.MakeGenericMethod(delegateType.GenericTypeArguments);
            var delegateMethod = concreteMethod.CreateDelegate(delegateType, target);
            return delegateMethod;
        }
    }
}
