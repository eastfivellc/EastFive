using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using EastFive.Linq;
using EastFive.Reflection;

namespace EastFive.Api.Serialization
{
    public class ExtrudeDictionarySafeConvert : ExtrudeConvert
    {

        public ExtrudeDictionarySafeConvert(IHttpRequest request, IApplication application)
            : base(request, application)
        {
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var valueType = value.GetType();
            if (valueType.IsSubClassOfGeneric(typeof(IDictionary<,>)))
            {
                writer.WriteStartArray();
                foreach (var kvpObj in value.DictionaryKeyValuePairs())
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("Key");
                    var keyValue = kvpObj.Key;
                    serializer.Serialize(writer, keyValue);
                    // WriteJson(writer, keyValue, serializer);

                    writer.WritePropertyName("Value");
                    var valueValue = kvpObj.Value;
                    //var valueJsonStr = JsonConvert.SerializeObject(valueValue, this);
                    ////var valueJObj = JObject.FromObject(valueValue, serializer);
                    //var valueJObj = JToken.Parse(valueJsonStr);
                    //valueJObj.WriteTo(writer, this);
                    serializer.Serialize(writer, valueValue);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                return;
            }
            base.WriteJson(writer, value, serializer);
        }
    }
}
