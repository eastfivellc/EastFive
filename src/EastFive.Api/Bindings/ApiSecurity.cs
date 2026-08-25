using EastFive.Extensions;
using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace EastFive.Api.Controllers
{
    [ApiSecurity]
    public struct ApiSecurity
    {
        public string key;
    }

    public class ApiSecurityAttribute : Attribute, IInstigatable, IBindApiParameter<string>
    {
        public const string accessKey = "accessKey";
        public const string apiKeySecurity = "ApiKeySecurity";

        public TResult Bind<TResult>(ParameterInfo parameter, string content,
                IApplication application,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure)
        {
            return onParsed(new Controllers.ApiSecurity 
            { 
                key = content,
            });
        }

        public Task<IHttpResponse> Instigate(IApplication httpApp,
                IHttpRequest request,
                ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            return EastFive.Web.Configuration.Settings.GetString(AppSettings.ApiKey,
                (authorizedApiKey) =>
                {
                    var queryParams = System.Web.HttpUtility.ParseQueryString(request.GetAbsoluteUri().Query);
                    if (queryParams[apiKeySecurity] == authorizedApiKey)
                        return onSuccess(new Controllers.ApiSecurity
                        { 
                            key = queryParams[apiKeySecurity],
                        });

                    var authorization = request.GetAuthorization();
                    if (authorization == authorizedApiKey)
                        return onSuccess(new Controllers.ApiSecurity 
                        { 
                            key = authorization,
                        });

                    return request.CreateResponse(HttpStatusCode.Unauthorized).AsTask();
                },
                (why) => request.CreateResponse(HttpStatusCode.Unauthorized).AddReason(why).AsTask());
        }
    }
}
