using EastFive.Analytics;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Web;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class InvokeApplicationRemote : InvokeApplication
    {
        public InvokeApplicationRemote(Uri serverUrl, string apiRouteName) : base(serverUrl, apiRouteName)
        {
        }

        public override Task<IHttpResponse> SendAsync(IHttpRequest httpRequest)
        {
            throw new NotImplementedException();
            //using (var client = new HttpClient())
            //{
            //    var response = await client.SendAsync(httpRequest);

            //    if (instigators.ContainsKey(response.StatusCode))
            //    {
            //        var instigator = instigators[response.StatusCode];
            //        var resultAttempt = await instigator(null, httpRequest, null,
            //            async (data) =>
            //            {
            //                var dataType = data.GetType();
            //                var invokeMethod = dataType.GetMethod("Invoke");
            //                var invokeParameters = invokeMethod.GetParameters();
            //                if (!invokeParameters.Any())
            //                {
            //                    var invokeResponseObj = (data as Delegate).DynamicInvoke(new object[] { });
            //                    var invokeResponse = invokeResponseObj as HttpResponseMessage;
            //                    return invokeResponse;
            //                }
            //                return await response.AsTask();
            //            });
            //        if (resultAttempt is InvokeApplicationExtensions.IReturnResult)
            //        {
            //            return resultAttempt;
            //        }
            //    }

            //    if (instigatorsGeneric.ContainsKey(response.StatusCode))
            //    {
            //        var instigatorGeneric = instigatorsGeneric[response.StatusCode];
            //        return await instigatorGeneric(default(Type), null, httpRequest, null,
            //            async (data) =>
            //            {
            //                var dataType = data.GetType();
            //                if (dataType.IsSubClassOfGeneric(typeof(CreatedBodyResponse<>)))
            //                {
            //                    var jsonString = await response.Content.ReadAsStringAsync();
            //                    var resourceType = dataType.GenericTypeArguments.First();
            //                    var converter = new Serialization.Converter();
            //                    var instance = Newtonsoft.Json.JsonConvert.DeserializeObject(
            //                        jsonString, resourceType, converter);
            //                    var responseDelegate = ((Delegate)data).DynamicInvoke(
            //                        instance, response.Content.Headers.ContentType.MediaType);
            //                    return (HttpResponseMessage)responseDelegate;
            //                }
            //                if (dataType.IsSubClassOfGeneric(typeof(ExecuteBackgroundResponseAsync)))
            //                {
            //                    //var jsonString = await response.Headers.();
            //                    //var resourceType = dataType.GenericTypeArguments.First();
            //                    //var converter = new RefConverter();
            //                    //var instance = Newtonsoft.Json.JsonConvert.DeserializeObject(
            //                    //    jsonString, resourceType, converter);
            //                    var responseDelegate = ((Delegate)data).DynamicInvoke(
            //                        new ExecuteContext());
            //                    return (HttpResponseMessage)responseDelegate;
            //                }
            //                return response;
            //            });
            //    }

            //    return response;
            //}
        }


        private Dictionary<HttpStatusCode, InstigatorDelegate> instigators =
            new Dictionary<HttpStatusCode, InstigatorDelegate>();

        public void SetInstigator(Type type, InstigatorDelegate instigator, bool clear = false)
        {
            if (type.ContainsCustomAttribute<HttpDelegateAttribute>())
            {
                var actionDelAttr = type.GetCustomAttribute<HttpDelegateAttribute>();
                var code = actionDelAttr.StatusCode;

                if (!clear)
                {
                    instigators.AddOrReplace(code, instigator);
                    return;
                }
                if (instigators.ContainsKey(code))
                    instigators.Remove(code);

            }
        }

        private Dictionary<HttpStatusCode, InstigatorDelegateGeneric> instigatorsGeneric =
            new Dictionary<HttpStatusCode, InstigatorDelegateGeneric>();

        public override IApplication Application => new RemoteApplication();

        public void SetInstigatorGeneric(Type type, InstigatorDelegateGeneric instigator,
            bool clear = false)
        {
            if (type.ContainsCustomAttribute<HttpDelegateAttribute>())
            {
                var actionDelAttr = type.GetCustomAttribute<HttpDelegateAttribute>();
                var code = actionDelAttr.StatusCode;
                if (!clear)
                {
                    instigatorsGeneric.AddOrReplace(code, instigator);
                    return;
                }
                if (instigatorsGeneric.ContainsKey(code))
                    instigatorsGeneric.Remove(code);
            }
        }

        protected override RequestMessage<TResource> BuildRequest<TResource>(IApplication application)
        {
            return new RequestMessage<TResource>(this);
        }

        public class ExecuteContext : IExecuteAsync
        {
            public bool ForceBackground => false;

            public Task<IHttpResponse> InvokeAsync(Action<double> updateCallback)
            {
                throw new NotImplementedException();
            }
        }

        private class RemoteApplication : IApplication
        {
            public ResourceInvocation[] Resources => throw new NotImplementedException();

            public IDictionary<Type, ConfigAttribute> ConfigurationTypes => throw new NotImplementedException();

            public ILogger Logger => throw new NotImplementedException();

            public object CastResourceProperty(object value, Type propertyType)
            {
                throw new NotImplementedException();
            }

            public TResult DoesStoreMonitoring<TResult>(Func<StoreMonitoringDelegate, TResult> onMonitorUsingThisCallback, Func<TResult> onNoMonitoring)
            {
                throw new NotImplementedException();
            }

            public TResult GetControllerType<TResult>(string routeName, Func<Type, TResult> onMethodsIdentified, Func<TResult> onKeyNotFound)
            {
                throw new NotImplementedException();
            }

            public IEnumerable<MethodInfo> GetExtensionMethods(Type controllerType)
            {
                throw new NotImplementedException();
            }

            public Task<IHttpResponse> Instigate(IHttpRequest request, ParameterInfo methodParameter, Func<object, Task<IHttpResponse>> onInstigated)
            {
                throw new NotImplementedException();
            }

            public void SetInstigator(Type type, InstigatorDelegate instigator, bool clear = false)
            {
                throw new NotImplementedException();
            }

            public void SetInstigatorGeneric(Type type, InstigatorDelegateGeneric instigator, bool clear = true)
            {
                throw new NotImplementedException();
            }
        }
    }

    
}
