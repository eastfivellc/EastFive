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
    public abstract class HttpGenericDelegateAttribute
        : InstigateGenericAttribute, IDocumentResponse
    {
        public virtual HttpStatusCode StatusCode { get; set; }

        public virtual string Example { get; set; }

        public override Task<IHttpResponse> InstigatorDelegateGeneric(Type type,
            IApplication httpApp, IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
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
            var statusCodeProp = attrType
                 .GetProperty("StatusCode", BindingFlags.Public | BindingFlags.Instance);
            if(statusCodeProp.CanWrite)
                statusCodeProp.SetValue(scope, StatusCode);

            return attrType
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

        protected override object CreateScope(Type type, IApplication httpApp,
            IHttpRequest request, ParameterInfo paramInfo)
        {
            var scope = base.CreateScope(type, httpApp, request, paramInfo);
            var statusCodeProperty = typeof(HttpGenericDelegateAttribute)
                .GetProperty("StatusCode", BindingFlags.Public | BindingFlags.Instance);
            if (statusCodeProperty.CanWrite)
                statusCodeProperty.SetValue(scope, this.StatusCode);
            return scope;
        }

        public virtual Response GetResponse(ParameterInfo paramInfo, HttpApplication httpApp)
        {
            var response = new Response()
            {
                ParamInfo = paramInfo,
                Name = paramInfo.Name,
                StatusCode = this.StatusCode,
                Example = this.Example,
                Headers = new KeyValuePair<string, string>[] { },
            };
            return paramInfo
                .GetAttributesInterface<IModifyDocumentResponse>()
                .Aggregate(response,
                    (last, attr) => attr.GetResponse(last, paramInfo, httpApp));
        }

    }

}
