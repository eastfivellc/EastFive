using Newtonsoft.Json;
using System.Linq;

namespace EastFive.Api.Serialization.Json
{
    public static class SerializationExtensions
    {
        public static string JsonSerialize<T>(this T obj,
            IApplication httpApp, IHttpRequest request)
        {
            var converter = new Serialization.ExtrudeConvert(request, httpApp);
            var jsonObj = JsonConvert.SerializeObject(obj,
                new JsonSerializerSettings
                {
                    Converters = new JsonConverter[] { converter }.ToList(),
                });

            return jsonObj;
        }
    }
}
