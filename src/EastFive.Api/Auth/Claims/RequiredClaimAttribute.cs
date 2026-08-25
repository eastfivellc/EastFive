using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using EastFive.Extensions;

namespace EastFive.Api.Auth
{
    public class RequiredClaimAttribute : Attribute, IHandleMethodInvocation
    {
        public virtual Uri ClaimType { get; set; }

        public virtual string ClaimValue { get; set; }

        public RequiredClaimAttribute(string requiredClaimType, string requiredClaimValue)
        {
            this.ClaimType = new Uri(requiredClaimType);
            this.ClaimValue = requiredClaimValue;
        }

        public RequiredClaimAttribute(string requiredClaimType,
            string[] requiredClaimValues)
        {
            this.ClaimType = new Uri(requiredClaimType);
            this.ClaimValue = requiredClaimValues.Join(',');
        }

        public Task<IHttpResponse> HandleMethodInvocationAsync(
            KeyValuePair<ParameterInfo, object>[] parameters,
            IReadOnlyDictionary<ParameterInfo, object> bindingContexts,
            MethodInfo method,
            IApplication httpApp,
            IHttpRequest request,
            InvokeMethodDelegate continueInvocation)
        {
            if (!request.IsAuthorizedFor(ClaimType, ClaimValue))
                return request
                    .CreateResponse(System.Net.HttpStatusCode.Forbidden)
                    .AddReason($"{method.DeclaringType.FullName}..{method.Name} requires claim `{ClaimType}`=`{this.ClaimValue}`")
                    .AsTask();
            return continueInvocation(parameters, bindingContexts, method, httpApp, request);
        }
    }
}
