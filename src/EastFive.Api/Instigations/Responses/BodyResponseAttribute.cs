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
    public class BodyResponseAttribute : HttpFuncDelegateAttribute
    {
        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            ContentResponse responseDelegate =
                (obj, contentType) =>
                {
                    var response = GetResponse(obj, contentType,
                        httpApp, request, parameterInfo);
                    return UpdateResponse(parameterInfo, httpApp, request, response);
                };
            return onSuccess(responseDelegate);
        }

        private IHttpResponse GetResponse(object obj, string contentType,
            IApplication httpApp, IHttpRequest request, ParameterInfo parameterInfo)
        {
            var objType = obj.GetType();
            if (!objType.ContainsAttributeInterface<IProvideSerialization>())
            {
                var response = request.CreateResponse(System.Net.HttpStatusCode.OK, obj);
                if (!contentType.IsNullOrWhiteSpace())
                    response.SetContentType(contentType);
                return response;
            }

            return new ProvidedHttpResponse(objType, obj,
                httpApp, request, parameterInfo,
                System.Net.HttpStatusCode.OK);
        }

        private class ProvidedHttpResponse : HttpResponse
        {
            public ProvidedHttpResponse(Type objType, object obj,
                IApplication httpApp, IHttpRequest request, ParameterInfo parameterInfo,
                HttpStatusCode statusCode) 
                : base(request, statusCode,
                    stream =>
                    {
                        var serializationProvider = objType
                            .GetAttributesInterface<IProvideSerialization>()
                            .OrderByDescending(x => x.GetPreference(request))
                            .First();
                        return serializationProvider.SerializeAsync(
                            stream, httpApp, request, parameterInfo, obj);
                    })
            {

            }
        }
    }
}

