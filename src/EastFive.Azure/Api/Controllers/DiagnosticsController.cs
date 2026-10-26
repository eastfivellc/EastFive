using Newtonsoft.Json;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using EastFive.Api;
using EastFive.Extensions;

namespace EastFive.Azure.Diagnostics
{
    public class AppSetting
    {
        [JsonProperty(PropertyName = "Name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "Value")]
        public string Value { get; set; }
    }

    [FunctionViewController(
        Namespace = "aadb2c",
        Route = "Diagnostics")]
    public class DiagnosticsController 
    {
        [EastFive.Api.HttpGet]
        public static Task<IHttpResponse> Get(EastFive.Api.Security security, IHttpRequest request)
        {
            return EastFive.Api.SuperAdminConfiguration.WithActorId(
                (actorIdSuperAdmin) =>
                {
                    if (actorIdSuperAdmin == security.performingAsActorId)
                    {
                        // The declared VALUE settings of every typed configuration; secrets are never read here.
                        var settings = EastFive.Configuration.ConfigurationCatalog.For(AppDomain.CurrentDomain.GetAssemblies())
                            .SelectMany(type => type.Members)
                            .Select(member => (member.Key, value: EastFive.Configuration.ConfigurationCatalog.CurrentValue(member)))
                            .Where(setting => setting.value != null)
                            .Select(setting => new AppSetting { Name = setting.Key, Value = setting.value })
                            .GroupBy(setting => setting.Name).Select(group => group.First())
                            .OrderBy(x => x.Name).ToArray();
                        return new JsonHttpResponse(request, System.Net.HttpStatusCode.OK, settings).AsTask<IHttpResponse>();
                    }
                    return request.CreateResponse(System.Net.HttpStatusCode.NotFound).AsTask();
                },
                (why) => request.CreateResponse(System.Net.HttpStatusCode.InternalServerError, why).AsTask());
        }
    }
}