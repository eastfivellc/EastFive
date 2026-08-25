using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using EastFive.Api.Resources;
using EastFive.Extensions;
using EastFive.Linq;

namespace EastFive.Api
{
    public abstract class InstigateGenericAttribute : InstigatableResponseAttribute, IInstigatableGeneric
    {
        public interface IDefineInstigateMethod
        {

        }

        public class InstigateMethodAttribute : Attribute, IDefineInstigateMethod
        {
        }

        protected Type type;
        protected IApplication httpApp;
        protected IHttpRequest request;
        protected ParameterInfo parameterInfo;

        public virtual Task<IHttpResponse> InstigatorDelegateGeneric(Type type,
                IApplication httpApp, IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            var scope = CreateScope(type, httpApp, request, parameterInfo);
            return this.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.ContainsAttributeInterface<IDefineInstigateMethod>())
                .First<MethodInfo, Task<IHttpResponse>>(
                    (multipartResponseMethodInfoGeneric, next) =>
                    {
                        var multipartResponseMethodInfoBound = multipartResponseMethodInfoGeneric
                            .MakeGenericMethod(type.GenericTypeArguments);
                        var responseDelegate = Delegate.CreateDelegate(type, scope, multipartResponseMethodInfoBound);
                        return onSuccess(responseDelegate);
                    },
                    () =>
                    {
                        throw new NotImplementedException();
                    });
        }

        protected virtual object CreateScope(Type type,
            IApplication httpApp, IHttpRequest request, ParameterInfo paramInfo)
        {
            var attrType = this.GetType();
            var scope = Activator.CreateInstance(attrType);
            attrType
                .GetField("type", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(scope, type);
            attrType
                .GetField("httpApp", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(scope, httpApp);
            attrType
                .GetField("request", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(scope, request);
            attrType
                .GetField("parameterInfo", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(scope, parameterInfo);
            return scope;
        }

    }

}
