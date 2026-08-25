using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using EastFive;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Linq.Async;
using EastFive.Reflection;
using Newtonsoft.Json;

namespace EastFive.Api.Serialization
{
    public class Converter : EastFive.Serialization.Json.Converter
    {
        private IHttpRequest request;

        public Converter(IHttpRequest request)
        {
            this.request = request;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is Type)
            {
                var typeValue = (value as Type);
                bool wroteWithoutBase = typeValue
                    .GetAttributesInterface<IProvideSerialization>()
                    .Where(x => x.ContentType.ToLowerNullSafe().Contains("json"))
                    .MaxOrEmpty(
                        x => x.GetPreference(this.request),
                        (serializationAttr, discardRank) =>
                        {
                            writer.WriteValue(serializationAttr.ContentType);
                            return true;
                        },
                        () =>
                        {
                            base.WriteJson(writer, value, serializer);
                            return false;
                        });
            }
            base.WriteJson(writer, value, serializer);
        }
    }

}
