using EastFive;
using EastFive.Linq;
using EastFive.Api.Bindings;
using EastFive.Collections.Generic;
using EastFive.Extensions;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Serialization
{
    public interface IBindJsonData
    {
        Type[] TypesCast { get; }

        bool CanConvert(Type objectType);
    }

    public class BindConvert : Newtonsoft.Json.JsonConverter
    {
        IHttpRequest request;
        HttpApplication application;
        IConvertJson[] jsonConverters;

        public BindConvert(IHttpRequest request, HttpApplication httpApplication)
        {
            this.request = request;
            this.application = httpApplication;
            this.jsonConverters = application.GetType()
                .GetAttributesInterface<IConvertJson>()
                .ToArray();
        }

        public override bool CanConvert(Type objectType)
        {
            // These will convert full objects, not just refs
            //if (type.IsSubClassOfGeneric(typeof(IReferenceable)))
            //    return true;
            //if (type.IsSubClassOfGeneric(typeof(IReferenceableOptional)))
            //    return true;

            if (objectType.IsSubClassOfGeneric(typeof(IRef<>)))
                return true;

            if (objectType.IsSubClassOfGeneric(typeof(IRefs<>)))
                return true;

            if (objectType.IsSubClassOfGeneric(typeof(IRefOptional<>)))
                return true;

            if (objectType.IsAssignableFrom(typeof(Guid)))
                return true;

            if (objectType == typeof(double))
                return true;

            if (objectType == typeof(decimal))
                return true;

            // Newtonsoft struggles with Nullable<T> where T : struct
            if (objectType.IsNullable())
                return true;

            if (objectType.IsAssignableFrom(typeof(IDictionary<,>)))
                return true;

            if (objectType == typeof(Type))
                return true;

            if (objectType == typeof(DateTime))
                return true;

            return jsonConverters
                .Any(jc => jc.CanConvert(objectType, this.request, this.application));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var converters = jsonConverters
                .Where(jc => jc.CanConvert(objectType, this.request, this.application));
            if (converters.Any())
            {
                return converters
                    .First()
                    .Read(reader, objectType, existingValue, serializer,
                        this.request, this.application);
            }

            return this.application.Bind(reader, objectType,
                v => v,
                (why) =>
                {
                    return existingValue;
                });
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException("BindConvert cannot write values.");
        }
    }

    public class JsonTokenParser : IParseToken
    {
        private JToken valueToken;

        public JsonTokenParser(JToken valueToken)
        {
            this.valueToken = valueToken;
        }

        public byte[] ReadBytes()
        {
            return valueToken.ToObject<byte[]>();
        }

        public Stream ReadStream()
        {
            return valueToken.ToObject<Stream>();
        }

        public bool IsString
        {
            get
            {
                return valueToken.Type == JTokenType.String;
            }
        }

        public string ReadString()
        {
            return valueToken.ToObject<string>();
        }

        public T ReadObject<T>()
        {
            if (valueToken is JObject)
            {
                var jObj = valueToken as Newtonsoft.Json.Linq.JObject;
                var jsonText = jObj.ToString();
                var value = JsonConvert.DeserializeObject<T>(jsonText);
                return value;
            }
            return valueToken.Value<T>();
        }

        public object ReadObject()
        {
            var token = valueToken;
            if (token is JValue)
            {
                if (token.Type == JTokenType.Boolean)
                    return token.Value<bool>();
                if (token.Type == JTokenType.Bytes)
                    return token.Value<byte[]>();
                if (token.Type == JTokenType.Date)
                    return token.Value<DateTime>();
                if (token.Type == JTokenType.Float)
                    return token.Value<float>();
                if (token.Type == JTokenType.Guid)
                    return token.Value<Guid>();
                if (token.Type == JTokenType.Integer)
                    return token.Value<int>();
                if (token.Type == JTokenType.None)
                    return null;
                if (token.Type == JTokenType.Null)
                    return null;
                if (token.Type == JTokenType.String)
                    return token.Value<string>();
                if (token.Type == JTokenType.Uri)
                    return token.Value<Uri>();
            }
            return token.ToObject<object>();
        }

        public IParseToken[] ReadArray()
        {
            if (valueToken.Type == JTokenType.Array)
                return (valueToken as JArray)
                    .Select(
                        token => new JsonTokenParser(token))
                    .ToArray();
            if (valueToken.Type == JTokenType.Null)
                return new IParseToken[] { };
            if (valueToken.Type == JTokenType.Undefined)
                return new IParseToken[] { };

            if (valueToken.Type == JTokenType.Object)
                return valueToken
                    .Children()
                    .Select(childToken => new JsonTokenParser(childToken))
                    .ToArray();

            if (valueToken.Type == JTokenType.Property)
            {
                var property = (valueToken as JProperty);
                return new JsonTokenParser(property.Value).AsArray();
            }

            return valueToken
                .Children()
                .Select(child => new JsonTokenParser(child))
                .ToArray();
        }

        public IDictionary<string, IParseToken> ReadDictionary()
        {
            KeyValuePair<string, IParseToken> ParseToken(JToken token)
            {
                if (token.Type == JTokenType.Property)
                {
                    var propertyToken = (token as JProperty);
                    return new JsonTokenParser(propertyToken.Value)
                        .PairWithKey<string, IParseToken>(propertyToken.Name);
                }
                return (new JsonTokenParser(token))
                    .PairWithKey<string, IParseToken>(token.ToString());
            }

            if (valueToken.Type == JTokenType.Array)
                return (valueToken as JArray)
                    .Select(ParseToken)
                    .ToDictionary();
            if (valueToken.Type == JTokenType.Null)
                return new Dictionary<string, IParseToken>();
            if (valueToken.Type == JTokenType.Undefined)
                return new Dictionary<string, IParseToken>();

            if (valueToken.Type == JTokenType.Object)
                return valueToken
                    .Children()
                    .Select(ParseToken)
                    .ToDictionary();

            return valueToken
                    .Children()
                    .Select(ParseToken)
                    .ToDictionary();
        }
    }
}
