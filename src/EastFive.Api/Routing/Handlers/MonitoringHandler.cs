using System;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using EastFive.Linq;
using System.Threading;

namespace EastFive.Api.Modules
{
    public class MonitoringHandler : ApplicationHandler
    {
        public MonitoringHandler(IApplication config)
            : base(config)
        {
        }

        private Task<IHttpResponse> StoreMonitoringInfoAndFireRequestAsync(IApplication httpApp,
            IHttpRequest request, CancellationToken cancellationToken, Guid authenticationId,
            Func<IHttpRequest, CancellationToken, Task<IHttpResponse>> continuation)
        {
            return GetControllerNameAndId(request, 
                async (controllerName, iden) =>
                {
                    var queryString = GetParamInfo(request, iden);
                    
                    return await httpApp.DoesStoreMonitoring(
                        async (monitoringCallback) =>
                        {
                            var createLogTask = monitoringCallback(Guid.NewGuid(), authenticationId,
                                DateTime.UtcNow, request.Method.ToString(), controllerName, queryString);

                            var httpResponseMessage = await continuation(request, cancellationToken);

                            await createLogTask;
                            return httpResponseMessage;
                        },
                        () => continuation(request, cancellationToken));
                },
                ()=> continuation(request, cancellationToken));
        }

        protected override Task<IHttpResponse> SendAsync(IApplication httpApplication,
            IHttpRequest request, CancellationToken cancellationToken,
            Func<IHttpRequest, CancellationToken, Task<IHttpResponse>> continuation)
        {
            return request.GetActorIdClaimsFromBearerParamAsync(
                (authenticationId, claims) => StoreMonitoringInfoAndFireRequestAsync(
                    httpApplication, request, cancellationToken, authenticationId, continuation),
                () =>
                {
                    return request.GetClaims(
                        (claimsEnumerable) =>
                        {
                            var claims = claimsEnumerable.ToArray();
                            var accountIdClaimTypeConfigurationSetting = EastFive.Api.AppSettings.ActorIdClaimType;
                            var accountIdClaimType = System.Configuration.ConfigurationManager.AppSettings[accountIdClaimTypeConfigurationSetting];
                            var result = claims.GetAccountIdAsync(
                                request, accountIdClaimType,
                                (authenticationId) => StoreMonitoringInfoAndFireRequestAsync(httpApplication, request, cancellationToken, authenticationId, continuation));
                            return result;
                        },
                        () => continuation(request, cancellationToken),
                        (why) => continuation(request, cancellationToken));
                },
                () => continuation(request, cancellationToken),
                () => continuation(request, cancellationToken));
        }

        private TResult GetControllerNameAndId<TResult>(IHttpRequest request,
            Func<string, string, TResult> onSuccess,
            Func<TResult> onUndetermined)
        {
            // controller and id
            if (request.RequestUri.Segments.Length == 4)
                return onSuccess(request.RequestUri.Segments[2], request.RequestUri.Segments[3]);

            // just controller
            if (request.RequestUri.Segments.Length == 3 )
                return onSuccess(request.RequestUri.Segments[2], string.Empty);

            if (request.RequestUri.Segments.Length == 2)
                return onSuccess(request.RequestUri.Segments[1], string.Empty);

            if (request.RequestUri.Segments.Length == 1)
                return onSuccess(request.RequestUri.Segments[0], string.Empty);

            return onUndetermined();
        }

        private string GetParamInfo(IHttpRequest request, string iden)
        {
            var queryParams = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);
            var queryElements = queryParams.AllKeys.Select(qP => $"{qP}:{queryParams[qP]}");

            if (!iden.IsNullOrWhiteSpace())
                queryElements = queryElements.Concat(new[] { $"Id:{iden}" });

            return queryElements.Join(" | ");
        }
    }
}
