using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Headers;

using EastFive.Linq;
using EastFive.Extensions;
using EastFive.Linq.Async;
using EastFive.Reflection;
using EastFive.Api.Resources;
using EastFive.Api.Meta.Flows;

namespace EastFive.Api
{
    [StatusCodeResponse(StatusCode = HttpStatusCode.Created)]
    public delegate IHttpResponse CreatedResponse();

    [BodyTypeResponse(StatusCode = HttpStatusCode.Created)]
    public delegate IHttpResponse CreatedBodyResponse<TResource>(TResource content, string contentType = default);
    
    public class BodyTypeResponseAttribute : HttpGenericDelegateAttribute,
        IProvideResponseType, IDefineWorkflowResponseVariable
    {
        public override string Example => "serialized object";

        [InstigateMethod]
        public IHttpResponse ContentResponse<TResource>(TResource content, 
            string contentTypeString = default(string))
        {
            var response = new ProvidedHttpResponse(typeof(TResource), content,
                this.httpApp, this.request, this.parameterInfo,
                this.StatusCode);
            return UpdateResponse(parameterInfo, httpApp, request, response);
        }

        private class ProvidedHttpResponse : EastFive.Api.HttpResponse
        {
            public const string DefaultType = "application/json";

            private IProvideSerialization serializationProvider;
            private object obj;

            private IHttpRequest request;
            private IApplication httpApp;
            private ParameterInfo parameterInfo;

            public ProvidedHttpResponse(Type objType, object obj,
                IApplication httpApp, IHttpRequest request, ParameterInfo parameterInfo,
                HttpStatusCode statusCode)
                : base(request, statusCode)
            {
                this.serializationProvider = (objType.IsArray? objType.GetElementType() : objType)
                    .GetAttributesInterface<IProvideSerialization>()
                    .OrderByDescending(x => x.GetPreference(request))
                    .First(
                            (provider, next) => provider,
                            () => default(IProvideSerialization));
                this.obj = obj;

                this.request = request;
                this.httpApp = httpApp;
                this.parameterInfo = parameterInfo;
            }

            public override void WriteHeaders(HttpContext context, ResponseHeaders headers)
            {
                base.WriteHeaders(context, headers);
                var contentType = GetContentType();

                headers.ContentType = new Microsoft.Net.Http.Headers.MediaTypeHeaderValue(contentType);

                string GetContentType()
                {
                    if (serializationProvider.IsDefaultOrNull())
                        return DefaultType;

                    var acceptsHeaders = context.Request.GetTypedHeaders().Accept
                        .NullToEmpty();

                    if (UseContentType())
                        return serializationProvider.ContentType;

                    if (UseMediaType())
                        return serializationProvider.MediaType;

                    return DefaultType;

                    bool UseContentType() => acceptsHeaders
                        .Where(
                            accept =>
                            {
                                var contentType = serializationProvider.ContentType;
                                if (contentType.IsNullOrWhiteSpace())
                                    return false;
                                return accept.MediaType.Equals(contentType,
                                    StringComparison.OrdinalIgnoreCase);
                            })
                        .Any();

                    bool UseMediaType() => acceptsHeaders
                        .Where(
                            accept =>
                            {
                                var mediaType = serializationProvider.MediaType;
                                if (mediaType.IsNullOrWhiteSpace())
                                    return false;
                                return accept.MediaType.Equals(mediaType,
                                    StringComparison.OrdinalIgnoreCase);
                            })
                        .Any();
                }

            }

            public override Task WriteResponseAsync(Stream stream)
            {
                if (serializationProvider.IsDefaultOrNull())
                    return JsonHttpResponse.WriteResponseAsync(stream, obj, request);
                
                return serializationProvider.SerializeAsync(
                        stream, httpApp, request, parameterInfo, obj);
            }
        }

        public override Response GetResponse(ParameterInfo paramInfo, HttpApplication httpApp)
        {
            var baseResponse = base.GetResponse(paramInfo, httpApp);
            if (paramInfo.ParameterType.GenericTypeArguments.IsSingle())
                baseResponse.IsMultipart = paramInfo.ParameterType.GenericTypeArguments.First().IsArray;
            
            return baseResponse;
        }

        public Type GetResponseType(ParameterInfo parameterInfo)
        {
            return parameterInfo.ParameterType.GenericTypeArguments.First();
        }

        public string[] GetInitializationLines(Response item, Method method)
        {
            return "let responseResource = pm.response.json();\r".AsArray();
        }

        public string[] GetScriptLines(
            string variableName, string propertyName,
            Response response, Method method)
        {
            return $"pm.environment.set(\"{variableName}\", responseResource.{propertyName});\r".AsArray();
        }
    }
}