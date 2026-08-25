using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Reflection;

using EastFive.Api.Resources;
using EastFive;
using EastFive.Extensions;
using EastFive.Reflection;
using EastFive.Text;
using EastFive.Collections.Generic;
using EastFive.Linq;
using EastFive.Serialization;

namespace EastFive.Api.Bindings
{
    public class StandardStringBindingsAttribute : Attribute,
        IBindParameter<string>, IBindApiParameter<string>,
        IBindParameter<byte[]>, IBindApiParameter<byte[]>
    {
        public delegate object BindingDelegate(
                StandardStringBindingsAttribute httpApp,
                string content,
            Func<object, object> onParsed,
            Func<string, object> notConvertable);

        #region Strings

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
            return BindDirect(type, content,
                onParsed,
                onDidNotBind: (why) =>
                {
                    if (application is IApiApplication)
                    {
                        var apiApplication = application as IApiApplication;
                        if (type == typeof(Type))
                        {
                            return apiApplication.GetResourceType(content,
                                type => onParsed(type),
                                () => onDidNotBind($"Could not find type:{content}"));
                        }
                    }
                    return onDidNotBind(why);
                },
                onBindingFailure);
        }

        public static TResult BindDirect<TResult>(Type type, string content, 
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure)
        {
            return content.BindTo(type,
                onParsed: onParsed,
                onDidNotBind: (why) =>
                {
                    return onDidNotBind($"No binding for type `{type.FullName}` provided by {typeof(StandardStringBindingsAttribute).FullName}.");
                },
                onBindingFailure: onBindingFailure);
        }

        #endregion

        #region Bytes

        public TResult Bind<TResult>(ParameterInfo parameter, byte [] content,
                IApplication application,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure) => Bind(parameter.ParameterType, content,
                application: application,
                onParsed: onParsed,
                onDidNotBind: onDidNotBind,
                onBindingFailure: onBindingFailure);

        public TResult Bind<TResult>(Type type, byte[] content,
                IApplication application,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure)
        {
            return BindDirect(type, content,
                onParsed,
                onDidNotBind,
                onBindingFailure);
        }

        public static TResult BindDirect<TResult>(Type type, byte[] content,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure)
        {
            if (type == typeof(Guid))
            {
                if (content.Length == 0x10)
                {
                    var guid = new Guid(content);
                    return onParsed(guid);
                }

                {
                    var byteSpan = (ReadOnlySpan<byte>)content;
                    var charSpan = MemoryMarshal.Cast<byte, char>(byteSpan);
                    if (Guid.TryParse(charSpan, out Guid guid))
                        return onParsed(guid);
                }
            }

            var stringValue = System.Text.Encoding.UTF8.GetString(content);
            return BindDirect(type, stringValue,
                onParsed,
                onDidNotBind,
                onBindingFailure);
        }

        #endregion

    }
}
