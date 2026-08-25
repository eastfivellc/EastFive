using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{

    public delegate Task<T> ReadRequestBodyDelegateAsync<T>();

    public interface IBindParameter<TProvider>
    {
        TResult Bind<TResult>(Type type, TProvider content,
                IApplication application,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure);
    }

    public interface IBindApiParameter<TProvider>
    {
        TResult Bind<TResult>(ParameterInfo parameter, TProvider content,
                IApplication application,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure);
    }

    public interface IBindApiPropertyOrField<TProvider>
    {
        TResult Bind<TResult>(MemberInfo fieldOrProperty, TProvider content,
                IApplication application,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure);
    }
}
