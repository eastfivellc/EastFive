using EastFive.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public delegate Task<IHttpResponse> InstigatorDelegate(
                HttpApplication httpApp, IHttpRequest routeData, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess);

    public delegate Task<IHttpResponse> InstigatorDelegateGeneric(
            Type type, HttpApplication httpApp, IHttpRequest routeData, ParameterInfo parameterInfo,
        Func<object, Task<IHttpResponse>> onSuccess);

    public delegate Task<TResult> ParseContentDelegateAsync<TResult>(
            ParameterInfo parameterInfo,
            IApplication httpApp, IHttpRequest routeData, 
        Func<object, TResult> onParsed,
        Func<string, TResult> onFailure);


    public delegate Task StoreMonitoringDelegate(Guid monitorRecordId, Guid authenticationId, DateTime when, string method, string controllerName, string queryString);

    public class ResourceInvocation
    {
        public IInvokeResource invokeResourceAttr;
        public Type type;
        public MethodInfo[] extensions;
    }

    public interface IApplication //: IInvokeApplication
    {
        ResourceInvocation[] Resources { get; }

        IDictionary<Type, ConfigAttribute> ConfigurationTypes { get; }

        EastFive.Analytics.ILogger Logger { get; }

        IEnumerable<MethodInfo> GetExtensionMethods(Type controllerType);

        object CastResourceProperty(object value, Type propertyType);

        void SetInstigator(Type type, InstigatorDelegate instigator, bool clear = false);

        TResult DoesStoreMonitoring<TResult>(
            Func<StoreMonitoringDelegate, TResult> onMonitorUsingThisCallback,
            Func<TResult> onNoMonitoring);

        void SetInstigatorGeneric(Type type, InstigatorDelegateGeneric instigator, bool clear = true);

        TResult GetControllerType<TResult>(string routeName,
            Func<Type, TResult> onMethodsIdentified,
            Func<TResult> onKeyNotFound);

        Task<IHttpResponse> Instigate(IHttpRequest request, 
                ParameterInfo methodParameter,
            Func<object, Task<IHttpResponse>> onInstigated);

    }

}
