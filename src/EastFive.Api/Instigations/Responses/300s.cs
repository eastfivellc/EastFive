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

using EastFive.Linq;
using EastFive.Extensions;
using EastFive.Linq.Async;
using EastFive.Reflection;
using EastFive.Api.Meta.Flows;
using EastFive.Api.Resources;

namespace EastFive.Api
{
    [RedirectResponse]
    public delegate IHttpResponse RedirectResponse(Uri redirectLocation);
    public class RedirectResponseAttribute : HttpFuncDelegateAttribute, IDefineWorkflowResponseVariable
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.Redirect;

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            RedirectResponse responseDelegate =
                (redirectLocation) =>
                {
                    var response = request.CreateRedirectResponse(redirectLocation);
                    return UpdateResponse(parameterInfo, httpApp, request, response);
                };
            return onSuccess(responseDelegate);
        }

        #region IDefineWorkflowResponseVariable

        public string[] GetInitializationLines(Response response, Method method)
        {
            var requireParser = "urlParser = require('postman-collection').Url.parse;";
            var redirectString = "let redirectStringToParse = pm.response.headers.get(\"Location\");";
            var redirUrl = "var parsedRedirectUrl = urlParser(redirectStringToParse);";
            var redirectDictionary = "var redirectDictionary = parsedRedirectUrl.query.reduce((a,x) => ({...a, [x.key]: x.value}), {});";

            return new string[]
            {
                requireParser,
                redirectString,
                redirUrl,
                redirectDictionary,
            };
        }

        public string[] GetScriptLines(string variableName, string propertyName, Response response, Method method)
        {
            var interstationVariableName = $"redirectParam_{variableName}";
            var lineExtract = $"var {interstationVariableName} = redirectDictionary[\"{propertyName}\"];\r";
            var lineMakeGlobal = $"pm.environment.set(\"{variableName}\", {interstationVariableName});\r";
            return new string[] { lineExtract, lineMakeGlobal, };
        }

        #endregion
    }

    [StatusCodeResponse(StatusCode = HttpStatusCode.NotModified)]
    public delegate IHttpResponse NotModifiedResponse();
}
