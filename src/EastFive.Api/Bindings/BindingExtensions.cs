using EastFive.Linq;
using EastFive.Reflection;
using EastFive.Api.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Bindings
{
    public static class BindingExtensions
    {
        public static TResult Bind<TProvider, TResult>(this IApplication application,
                TProvider provider, ParameterInfo parameter,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailureToBind)
        {
            var parameterType = parameter.ParameterType;

            // Precedence: parameter → application → parameter type. The chain is
            // lazy, so later locations are only scanned when earlier binders decline
            // (each binder calls next() from its onDidNotBind arm).
            return parameter.AttributeInterfacesInParameter<IBindApiParameter<TProvider>>()
                .Concat(application.AttributeInterfacesInApplication<IBindApiParameter<TProvider>>())
                .Concat(parameterType.AttributeInterfacesInType<IBindApiParameter<TProvider>>(inherit: true))
                .First(
                    (paramBinder, next) =>
                    {
                        return paramBinder.Bind(parameter, provider,
                                application,
                            onParsed,
                            (why) => next(),
                            (why) => onFailureToBind(why));
                    },
                    () =>
                    {
                        return application.Bind(provider, parameterType,
                            onParsed,
                            onFailureToBind);
                    });
        }

        public static TResult Bind<TProvider, TResult>(this IApplication application,
                TProvider provider, MemberInfo propertyOrFieldInfo,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailureToBind)
        {
            return propertyOrFieldInfo
                .GetAttributesInterface<IBindApiPropertyOrField<TProvider>>()
                .First(
                    (propBinder, next) =>
                    {
                        return propBinder.Bind<TResult>(propertyOrFieldInfo, provider,
                                application,
                            onParsed,
                            (why) =>
                            {
                                return next();
                            },
                            (why) => onFailureToBind(why));
                    },
                    () =>
                    {
                        var type = propertyOrFieldInfo.GetPropertyOrFieldType();
                        return application.Bind(provider, type,
                            onParsed,
                            onFailureToBind);
                    });
        }

        public static TResult Bind<TProvider, TResult>(this IApplication application,
                TProvider provider, Type type,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailureToBind)
        {
            return application.GetType()
                .GetAttributesInterface<IBindParameter<TProvider>>(true)
                .First(
                    (paramBinder, next) =>
                    {
                        return paramBinder.Bind(type, provider,
                                application,
                            onParsed,
                            (why) =>
                            {
                                return next();
                            },
                            onFailureToBind);
                    },
                    () =>
                    {
                        return type.Bind(provider, application,
                            onParsed: onParsed,
                            onDidNotBind:
                                () =>
                                {
                                    var providerType = provider==null ?
                                        typeof(TProvider).FullName
                                        :
                                        provider.GetType().FullName;
                                    return onFailureToBind(
                                        $"{type.FullName} does not have binding attribute that can bind from {providerType}");
                                },
                            onFailureToBind: onFailureToBind);
                    });
        }

        public static TResult Bind<TProvider, TResult>(this Type type,
                TProvider provider, IApplication application,
            Func<object, TResult> onParsed,
            Func<TResult> onDidNotBind,
            Func<string, TResult> onFailureToBind)
        {
            return type
                .GetAttributesInterface<IBindParameter<TProvider>>(true)
                .First(
                    (paramBinder, next) =>
                    {
                        return paramBinder.Bind(type, provider,
                                application,
                            onParsed,
                            (why) =>
                            {
                                return next();
                            },
                            onFailureToBind);
                    },
                    () => onDidNotBind());
        }
    }
}
