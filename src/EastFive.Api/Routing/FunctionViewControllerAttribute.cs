using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

using EastFive.Api.Bindings;
using EastFive.Api.Core;
using EastFive.Api.Resources;
using EastFive.Api.Routing;
using EastFive.Api.Serialization;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Linq.Async;
using System.IO;
using Newtonsoft.Json;
using EastFive.Api.Bindings.ContentHandlers;

namespace EastFive.Api
{
    public class FunctionViewControllerAttribute 
        : Attribute, IInvokeResource, IDocumentRoute, IProvideSerialization
    {
        private string ns;
        public string Namespace
        {
            get
            {
                if (ns.HasBlackSpace())
                    return ns;
                return "api";
            }
            set
            {
                ns = value;
            }
        }

        public string ExcludeNamespaces { get; set; }
        public string Route { get; set; }

        private string contentType;
        public string ContentType
        {
            get
            {
                if (contentType.HasBlackSpace())
                    return contentType;
                var routeHyphenCase = this.Route.ToHypenCase();
                var routeRenamed = $"x-application/{routeHyphenCase}";
                return routeRenamed;
            }
            set
            {
                this.contentType = value;
            }
        }

        private const double defaultPreference = -111;

        public double Preference { get; set; } = defaultPreference;

        public double GetPreference(IHttpRequest request)
        {
            if (Preference != defaultPreference)
                return Preference;

            return 0.0;
        }

        public string ContentTypeVersion { get; set; }
        public string [] ContentTypeEncodings { get; set; }

        public Uri GetRelativePath(Type resourceDecorated)
        {
            var routeDirectory = this.Namespace.HasBlackSpace() ?
                this.Namespace
                :
                Web.Configuration.Settings.GetString(
                        AppSettings.DefaultNamespace,
                    (ns) => ns,
                    (whyUnspecifiedOrInvalid) => "api");

            var route = this.Route.HasBlackSpace() ?
                this.Route
                :
                resourceDecorated.Name;

            return new Uri($"{routeDirectory}/{route}", UriKind.Relative);
        }

        internal static Task<IHttpResponse> InvokeHandledMethodAsync(
            IApplication httpApp, IApplicationHandlers handlers, IHttpRequest routeData,
            Type controllerType, MethodInfo method,
            KeyValuePair<ParameterInfo, object>[] queryParameters)
        {
            var queryParameterOptions = queryParameters.ToDictionary(kvp => kvp.Key.Name, kvp => kvp.Value);
            return method.GetParameters()
                .SelectReduce(
                    async (ParameterInfo methodParameter, Func<object, Task<IHttpResponse>> next) =>
                    {
                        if (queryParameterOptions.ContainsKey(methodParameter.Name))
                            return await next(queryParameterOptions[methodParameter.Name]);

                        return await httpApp.Instigate(routeData, methodParameter, next);
                    },
                    async (object[] methodParameters) =>
                    {
                        try
                        {
                            if (method.IsGenericMethodDefinition)
                            {
                                method = method.MakeGenericMethod(controllerType.AsArray());
                                // var genericArguments = method.GetGenericArguments().Select(arg => arg.Name).Join(",");
                                //return routeData.CreateResponse(HttpStatusCode.InternalServerError)
                                //    .AddReason($"Could not invoke {method.DeclaringType.FullName}..{method.Name} because it contains generic arguments:{genericArguments}");
                            }

                            var response = method.Invoke(null, methodParameters);
                            if (typeof(Api.IHttpResponse).IsAssignableFrom(method.ReturnType))
                                return ((Api.IHttpResponse)response);
                            if (typeof(Task<Api.IHttpResponse>).IsAssignableFrom(method.ReturnType))
                                return (await (Task<Api.IHttpResponse>)response);
                            if (typeof(Task<Task<Api.IHttpResponse>>).IsAssignableFrom(method.ReturnType))
                                return (await await (Task<Task<Api.IHttpResponse>>)response);

                            return (routeData.CreateResponse(System.Net.HttpStatusCode.InternalServerError)
                                .AddReason($"Could not convert type: {method.ReturnType.FullName} to HttpResponseMessage."));
                        }
                        catch (TargetInvocationException ex)
                        {
                            var paramList = methodParameters
                                .Where(p => p != null)
                                .Select(p => p.GetType().FullName).Join(",");
                            var body = ex.InnerException.IsDefaultOrNull() ?
                                ex.StackTrace
                                :
                                $"[{ex.InnerException.GetType().FullName}]{ex.InnerException.Message}:\n{ex.InnerException.StackTrace}";
                            return routeData
                                .CreateResponse(HttpStatusCode.InternalServerError, body)
                                .AddReason($"Could not invoke {method.DeclaringType.FullName}.{method.Name}({paramList})");
                        }
                        catch (Exception ex)
                        {
                            return await handlers.ExceptionHandlers
                                .Aggregate(
                                    (Exception exFinal, MethodInfo methodFinal, KeyValuePair<ParameterInfo, object>[] queryParametersFinal, IApplication httpAppFinal, IHttpRequest routeDataFinal) =>
                                    {
                                        if (ex is IHttpResponseMessageException)
                                        {
                                            var httpResponseMessageException = ex as IHttpResponseMessageException;
                                            return httpResponseMessageException.CreateResponseAsync(
                                                httpApp, routeDataFinal, queryParameterOptions,
                                                method, methodParameters).AsTask<Api.IHttpResponse>();
                                        }

                                        return routeData
                                            .CreateResponse(HttpStatusCode.InternalServerError)
                                            .AddReason(ex.Message)
                                            .AsTask<Api.IHttpResponse>();
                                    },
                                    (HandleExceptionDelegate callback, IHandleExceptions methodHandler) =>
                                    {
                                        return (Exception exCurrent, MethodInfo methodCurrent, KeyValuePair<ParameterInfo, object>[] queryParametersCurrent, IApplication httpAppCurrent, IHttpRequest requestCurrent) =>
                                            methodHandler.HandleExceptionAsync(exCurrent, methodCurrent,
                                            queryParametersCurrent, httpAppCurrent, requestCurrent,
                                            callback);
                                    })
                                .Invoke(ex, method, queryParameters, httpApp, routeData);
                        }

                    });
        }

        public virtual Route GetRoute(Type type, HttpApplication httpApp)
        {
            var actionMethods = type
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Concat(httpApp.GetExtensionMethods(type))
                .Where(method => method.ContainsAttributeInterface<IMatchRoute>(true))
                .ToArray();

            var ns = this.Namespace.HasBlackSpace() ? this.Namespace : "api";
            return new Route(type, ns, this.Route,
                actionMethods,
                type.GetMembers(BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance),
                httpApp);
        }

        #region Serialization

        public string MediaType => "application/json";

        public Task SerializeAsync(Stream responseStream,
            IApplication httpApp, IHttpRequest request, ParameterInfo paramInfo, object obj)
        {
            var converter = new Serialization.ExtrudeConvert(request, httpApp);
            var jsonObj = Newtonsoft.Json.JsonConvert.SerializeObject(obj,
                new JsonSerializerSettings
                {
                    Converters = new JsonConverter[] { converter }.ToList(),
                    ContractResolver = Serialization.Json.ApiPropertyContractResolver.Instance,
                });
            var contentType = this.ContentType.HasBlackSpace() ?
                this.ContentType
                :
                this.MediaType;
            return responseStream.WriteResponseText(jsonObj, request);
            //using (var streamWriter = request.TryGetAcceptEncoding(out Encoding writerEncoding) ?
            //    new StreamWriter(responseStream, writerEncoding)
            //    :
            //    new StreamWriter(responseStream, Encoding.UTF8))
            //{
            //    await streamWriter.WriteAsync(jsonObj);
            //    await streamWriter.FlushAsync();
            //}
        }

        #endregion

        #region Collection parameters

        public delegate TResult ParseContentDelegate<TResult>(Type type,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure);

        protected struct MethodCast
        {
            public bool valid;
            public string[] extraBodyParams;
            public string[] extraQueryParams;
            public SelectParameterResult[] failedValidations;
            public MethodInfo method;

            public string ErrorMessage
            {
                get
                {
                    var failedValidationErrorMessages = failedValidations
                        .Select(
                            paramResult =>
                            {
                                var validator = paramResult.parameterInfo.GetAttributeInterface<IBindApiValue>();
                                var lookupName = validator.GetKey(paramResult.parameterInfo);
                                var location = paramResult.Location;
                                return $"{lookupName}({location}):{paramResult.failure}";
                            })
                        .ToArray();

                    var contentFailedValidations = failedValidationErrorMessages.Any() ?
                        $"Please correct the values for [{failedValidationErrorMessages.Join(",")}]"
                        :
                        "";

                    var extraParamMessages = extraQueryParams
                        .NullToEmpty()
                        .Select(extraQueryParam => $"{extraQueryParam}(QUERY)")
                        .Concat(
                            extraBodyParams
                                .NullToEmpty()
                                .Select(extraBodyParam => $"{extraBodyParam}(BODY)"));
                    var contentExtraParams = extraParamMessages.Any() ?
                        $"emove parameters [{extraParamMessages.Join(",")}]."
                        :
                        "";

                    if (contentFailedValidations.IsNullOrWhiteSpace())
                    {
                        if (contentExtraParams.IsNullOrWhiteSpace())
                            return "Query validation failure";

                        return $"R{contentExtraParams}";
                    }

                    if (contentExtraParams.IsNullOrWhiteSpace())
                        return contentFailedValidations;

                    return $"{contentFailedValidations} and r{contentExtraParams}";
                }
            }
        }

        #endregion

    }
}
