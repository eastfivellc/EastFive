using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace EastFive.Api
{
    public class JsonConvertGraphicsAttribute : Attribute, IConvertJson
    {
        public bool CanConvert(Type objectType, IHttpRequest httpRequest, IApplication application)
        {
            return objectType == typeof(RectangleF);
        }

        public object Read(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer, IHttpRequest httpRequest, IApplication application)
        {
            var rect = new RectangleF();
            if (reader.TokenType != JsonToken.StartObject)
                return rect;
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject)
                    break;
                if (reader.TokenType != JsonToken.PropertyName)
                    continue;
                if ("x".Equals(reader.Path, StringComparison.OrdinalIgnoreCase))
                {
                    rect.X = ReadFloat(rect.X);
                    continue;
                }
                if ("y".Equals(reader.Path, StringComparison.OrdinalIgnoreCase))
                {
                    rect.Y = ReadFloat(rect.Y);
                    continue;
                }
                if ("width".Equals(reader.Path, StringComparison.OrdinalIgnoreCase))
                {
                    rect.Width = ReadFloat(rect.Width);
                    continue;
                }
                if ("height".Equals(reader.Path, StringComparison.OrdinalIgnoreCase))
                {
                    rect.Height = ReadFloat(rect.Height);
                    continue;
                }

                float ReadFloat(float noOpValue)
                {
                    if (!reader.Read())
                        return noOpValue;

                    if (reader.TokenType == JsonToken.Integer)
                    {
                        var intValue = (int)reader.Value;
                        return (float)intValue;
                    }
                    if (reader.TokenType == JsonToken.Float)
                    {
                        var floatValue = (float)reader.Value;
                        return floatValue;
                    }

                    return noOpValue;
                }
            }
            return rect;
        }

        public void Write(JsonWriter writer, object value, JsonSerializer serializer,
            IHttpRequest httpRequest, IApplication application)
        {
            var rectF = (RectangleF)value;
            writer.WriteStartObject();
            writer.WritePropertyName("x");
            writer.WriteValue(rectF.X);
            writer.WritePropertyName("y");
            writer.WriteValue(rectF.Y);
            writer.WritePropertyName("width");
            writer.WriteValue(rectF.Width);
            writer.WritePropertyName("height");
            writer.WriteValue(rectF.Height);
            writer.WriteEndObject();
        }
    }
}
