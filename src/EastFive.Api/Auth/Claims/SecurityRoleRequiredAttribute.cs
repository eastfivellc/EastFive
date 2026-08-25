using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Web.Configuration;

namespace EastFive.Api.Auth
{
    public class SecurityRoleRequiredAttribute : Attribute, IHandleMethodInvocation
    {
        public virtual string [] RolesAllowed { get; set; }

        public virtual string[] RolesDenied { get; set; }

        public bool AllowLocalHost { get; set; } = false;

        public static bool allowLocalHostGlobal = EastFive.Api.AppSettings.Auth.AllowLocalHostGlobalSecurityRole
            .ConfigurationBoolean(
                allow => allow,
                onFailure: (why) => false,
                onNotSpecified: () => false);

        public virtual StringComparison Comparison { get; set; } = StringComparison.OrdinalIgnoreCase;

        public Task<IHttpResponse> HandleMethodInvocationAsync(
            KeyValuePair<ParameterInfo, object>[] parameters,
            IReadOnlyDictionary<ParameterInfo, object> bindingContexts,
            MethodInfo method,
            IApplication httpApp,
            IHttpRequest request,
            InvokeMethodDelegate continueInvocation)
        {
            var claims = request.GetClaims(
                cs => cs.ToArray(),
                authorizationNotSet: () => new Claim[] { },
                failure: (why) => new Claim[] { });
            var roles = claims
                .Where(claim => String.Equals(ClaimTypes.Role, claim.Type, StringComparison.OrdinalIgnoreCase))
                .First(
                    (claim, next) =>
                    {
                        return claim.Value.Split(',');
                    },
                    () =>
                    {
                        return new string[] { };
                    });
            Func<string, bool> roleInterigator = (role) =>
            {
                return roles
                    .Where(r => String.Equals(role, r, Comparison))
                    .Any();
            };

            return ProcessClaimsAsync(roleInterigator);

            Task<IHttpResponse> ProcessClaimsAsync(Func<string, bool> doesContainRole)
            {
                return RolesDenied
                    .NullToEmpty()
                    .Where(rollAllowed => doesContainRole(rollAllowed))
                    .First(
                        (rollDenied, next) =>
                        {
                            return DenyAsync("denies", rollDenied);
                        },
                        () =>
                        {
                            if (!RolesAllowed.Any())
                                return continueInvocation(parameters, bindingContexts, method, httpApp, request);

                            return RolesAllowed
                                .Where(rollAllowed => doesContainRole(rollAllowed))
                                .First(
                                    (rollAllowed, next) => continueInvocation(parameters, bindingContexts, method, httpApp, request),
                                    () => DenyAsync("requires one of", RolesAllowed.Join(',')));
                        });
            }


            Task<IHttpResponse> DenyAsync(string action, string equals)
            {
                if (AllowLocalHost || allowLocalHostGlobal)
                    if (request.IsLocalHostRequest())
                        return continueInvocation(parameters, bindingContexts, method, httpApp, request);

                return request
                    .CreateResponse(System.Net.HttpStatusCode.Forbidden)
                    .AddReason($"{method.DeclaringType.FullName}..{method.Name} {action} role claim ({ClaimTypes.Role}) = `{equals}`")
                    .AsTask();
            }
        }
    }
}
