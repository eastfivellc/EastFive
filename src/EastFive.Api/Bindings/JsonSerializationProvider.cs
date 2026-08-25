using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using EastFive.Api.Bindings.ContentHandlers;

namespace EastFive.Api
{
    public class JsonSerializationProviderAttribute : Attribute, IProvideSerialization
    {
        public string MediaType => "application/json";

        public string ContentType => MediaType;

        private const double defaultPreference = -111;

        public double Preference { get; set; } = defaultPreference;

        public double GetPreference(IHttpRequest request)
        {
            if (Preference != defaultPreference)
                return Preference;

            var accepts = request.GetAcceptTypes();
            var matches = accepts.Where(mt => mt.MediaType.ToLower() == this.MediaType);
            if (matches.Any())
            {
                var match = matches.First();
                if (match.Quality.HasValue)
                    return match.Quality.Value;
                return 0.5;
            }

            if (accepts.Where(mt => mt.MediaType.ToLower().Contains("json")).Any())
                return 0.2;

            return -0.1;
        }

        public virtual Task SerializeAsync(Stream responseStream,
            IApplication httpApp, IHttpRequest request, ParameterInfo paramInfo, object obj)
        {
            var converter = new Serialization.ExtrudeConvert(request, httpApp);
            var jsonObj = Newtonsoft.Json.JsonConvert.SerializeObject(obj,
                new JsonSerializerSettings
                {
                    Converters = new JsonConverter[] { converter }.ToList(),
                    ContractResolver = Serialization.Json.ApiPropertyContractResolver.Instance,
                });
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
    }

    public class JsonSerializationDictionarySafeProviderAttribute : JsonSerializationProviderAttribute
    {
        public override Task SerializeAsync(Stream responseStream,
            IApplication httpApp, IHttpRequest request, ParameterInfo paramInfo, object obj)
        {
            var converter = new Serialization.ExtrudeDictionarySafeConvert(request, httpApp);
            var jsonObj = Newtonsoft.Json.JsonConvert.SerializeObject(obj,
                new JsonSerializerSettings
                {
                    Converters = new JsonConverter[] { converter }.ToList(),
                    ContractResolver = Serialization.Json.ApiPropertyContractResolver.Instance,
                });
            return responseStream.WriteResponseText(jsonObj, request);
            //var streamWriter = request.TryGetAcceptEncoding(out Encoding writerEncoding) ?
            //    new StreamWriter(responseStream, writerEncoding)
            //    :
            //    new StreamWriter(responseStream, Encoding.UTF8);
            //return streamWriter.WriteAsync(jsonObj);
        }
    }
}
