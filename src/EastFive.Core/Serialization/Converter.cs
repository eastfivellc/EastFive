using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using EastFive.Extensions;
using EastFive.Linq.Async;
using EastFive.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EastFive.Serialization.Json
{
    public class Converter : Newtonsoft.Json.JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            if (objectType.IsSubClassOfGeneric(typeof(IRefOptional<>)))
                return true;
            if (objectType.IsSubClassOfGeneric(typeof(IDictionary<,>)))
                return true;
            if (objectType.IsSubclassOf(typeof(Type)))
                return true;
            if (objectType.IsSubClassOfGeneric(typeof(IRef<>)))
                return true;
            if (objectType.IsSubClassOfGeneric(typeof(IRefOptional<>)))
                return true;
            if (objectType.IsSubClassOfGeneric(typeof(IRefs<>)))
                return true;
            if (objectType.IsEnum)
                return true;
            //if (objectType == typeof(byte[]))
            //    return true;
            // THis doesn't work because it will serialize the whole object as a single GUID
            //if (objectType.IsSubClassOfGeneric(typeof(IReferenceable)))
            //    return true;
            return false;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            => ReadJsonStatic(reader, objectType, existingValue, serializer);

        public static object ReadJsonStatic(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (objectType == typeof(string))
            {
                if (reader.TokenType == JsonToken.String)
                {
                    var stringValue = reader.Value as string;
                    return stringValue;
                }
                if (reader.TokenType == JsonToken.Null)
                {
                    return null;
                }
            }

            if (objectType.IsSubClassOfGeneric(typeof(IReferenceable)))
            {
                if (objectType.IsSubClassOfGeneric(typeof(IRef<>)))
                {
                    var id = GetGuid();
                    if (id.IsDefault())
                        return existingValue;
                    var refType = typeof(Ref<>).MakeGenericType(objectType.GenericTypeArguments);
                    return Activator.CreateInstance(refType, id);
                }
            }

            if (objectType.IsSubClassOfGeneric(typeof(IReferenceableOptional)))
            {
                if (objectType.IsSubClassOfGeneric(typeof(IRefOptional<>)))
                {
                    var id = GetGuidMaybe();

                    if (id == null)
                        return RefOptionalHelper.CreateEmpty(objectType.GenericTypeArguments.First());

                    var refType = typeof(RefOptional<>).MakeGenericType(objectType.GenericTypeArguments);
                    return Activator.CreateInstance(refType, id);
                }
            }

            if (objectType.IsSubClassOfGeneric(typeof(IReferences)))
            {
                if (objectType.IsSubClassOfGeneric(typeof(IRefs<>)))
                {
                    var ids = GetGuids();
                    var refType = typeof(Refs<>).MakeGenericType(objectType.GenericTypeArguments);
                    return Activator.CreateInstance(refType, ids);
                }
            }

            if (objectType.IsSubClassOfGeneric(typeof(IDictionary<,>)))
            {

                var dictionaryType = typeof(Dictionary<,>).MakeGenericType(objectType.GenericTypeArguments);
                var instance = Activator.CreateInstance(dictionaryType);
                
                if (reader.TokenType == JsonToken.StartArray)
                {
                    var addMethod = dictionaryType.GetMethod("Add", BindingFlags.Public | BindingFlags.Instance);
                    while(reader.Read())
                    {
                        if (reader.TokenType == JsonToken.EndArray)
                            return instance;

                        if (reader.TokenType != JsonToken.StartObject)
                        {
                            Console.WriteLine($"{reader.TokenType} != JsonToken.StartObject");
                            continue;
                        }
                        if (!reader.Read())
                        {
                            Console.WriteLine($"Reader terminated w/o finding JsonToken.EndArray");
                            return instance;
                        }

                        if (!GetValue("key", objectType.GenericTypeArguments.First(), out object keyValue))
                            continue;

                        if (!GetValue("value", objectType.GenericTypeArguments.Last(), out object valueValue))
                            continue;

                        addMethod.Invoke(instance, new object[] { keyValue, valueValue });

                        if (reader.TokenType != JsonToken.EndObject)
                            Console.WriteLine($"{reader.TokenType} != JsonToken.EndObject");
                        //if (!reader.Read())
                        //{
                        //    Console.WriteLine($"Reader terminated w/o finding JsonToken.EndArray (after kvp).");
                        //    return instance;
                        //}
                        bool GetValue(string expectedName, Type type, out object value)
                        {
                            if (reader.TokenType != JsonToken.PropertyName)
                            {
                                value = null;
                                return false;
                            }

                            var jprop = JProperty.Load(reader);
                            //Console.WriteLine($"Property[{jprop.Name}] = ({type.FullName}) {jprop.Value.Value<string>()}");
                            var name = jprop.Name;
                            var valueReader = jprop.Value.CreateReader();
                            value = serializer.Deserialize(valueReader, type);
                            if (expectedName.Equals(name, StringComparison.InvariantCultureIgnoreCase))
                                return true;

                            Console.WriteLine($"`{expectedName}` != `{name}`.");
                            return false;
                        }
                    }
                    return instance;
                }

                if (reader.TokenType == JsonToken.StartObject)
                {
                    if (!reader.Read())
                        return instance;
                    var addMethod = dictionaryType.GetMethod("Add", BindingFlags.Public | BindingFlags.Instance);
                    do
                    {
                        Console.WriteLine($"{reader.Path} = ({reader.TokenType}) {reader.Value}");
                        if (reader.TokenType == JsonToken.EndObject)
                            return instance;

                        if (reader.TokenType == JsonToken.PropertyName)
                        {
                            var jprop = JProperty.Load(reader);
                            var type = objectType.GenericTypeArguments.Last();
                            Console.WriteLine($"Property[{jprop.Name}] = ({type.FullName}) {jprop.Value.Value<string>()}");
                            var name = jprop.Name;
                            var value = serializer.Deserialize(jprop.Value.CreateReader(), type);
                            //var value = GetValue();
                            //object GetValue()
                            //{
                            //    if (jprop.Value.Type == JTokenType.String)
                            //        return jprop.Value.Value<string>();

                            //    if (jprop.Value.Type == JTokenType.Guid)
                            //        return jprop.Value.Value<Guid>();

                            //    return jprop.Value.Value<object>();
                            //}
                            //var value = serializer.Deserialize(reader, type);
                            addMethod.Invoke(instance, new object[] { name, value });
                            continue;
                        }
                        else
                        {
                            Console.WriteLine($"Using path for {reader.TokenType}: {reader.Path} = {reader.Value}");
                            addMethod.Invoke(instance, new object[] { reader.Path, reader.Value });
                            if (!reader.Read())
                                return instance;
                        }
                    } while (true);
                }
                return instance;
            }

            if (objectType == typeof(byte[]))
            {
                if (reader.TokenType == JsonToken.String)
                {
                    var bytesString = reader.Value as string;
                    return bytesString.FromBase64String();
                }
            }

            if (objectType == typeof(Type))
            {
                if (reader.TokenType == JsonToken.String)
                {
                    var bytesString = reader.Value as string;
                    return bytesString.GetClrType(
                        matched => matched,
                        () => default(Type));
                }
                return default(Type);
            }

            if (objectType.IsEnum)
            {
                if (reader.TokenType == JsonToken.String)
                {
                    var enumString = reader.Value as string;
                    if (Enum.TryParse(objectType, enumString, out object enumValue))
                        return enumValue;
                }
            }

            return existingValue;


            Guid GetGuid()
            {
                if (reader.TokenType == JsonToken.String)
                {
                    var guidString = reader.Value as string;
                    return Guid.Parse(guidString);
                }
                if(reader.TokenType==JsonToken.Null)
                {
                    return default(Guid);
                }
                throw new Exception();
            }

            Guid? GetGuidMaybe()
            {
                if (reader.TokenType == JsonToken.Null)
                    return default(Guid?);
                return GetGuid();
            }

            Guid[] GetGuids()
            {
                if (reader.TokenType == JsonToken.Null)
                    return new Guid[] { };

                IEnumerable<Guid> Enumerate()
                {
                    while (reader.TokenType != JsonToken.EndArray)
                    {
                        if (!reader.Read())
                            yield break;
                        var guidStr = reader.ReadAsString();
                        yield return Guid.Parse(guidStr);
                    }
                }
                return Enumerate().ToArray();
            }

        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (!value.TryGetType(out Type valueType))
            {
                writer.WriteNull();
                return;
            }

            // IsSubClassOfGeneric(typeof(IReferenceable)) doesn't work because 
            // it will serialize the whole object as a single GUID 
            // (if value is IReferenceable)
            if (valueType.IsSubClassOfGeneric(typeof(IRef<>)))
            {
                var id = (value as IReferenceable).id;
                writer.WriteValue(id);
                return;
            }

            if (value is IReferences)
            {
                writer.WriteStartArray();
                Guid[] ids = (value as IReferences).ids
                    .Select(
                        id =>
                        {
                            writer.WriteValue(id);
                            return id;
                        })
                    .ToArray();
                writer.WriteEndArray();
                return;
            }
            if (value is IReferenceableOptional)
            {
                var id = (value as IReferenceableOptional).id;
                writer.WriteValue(id);
                return;
            }
            if (valueType.IsSubClassOfGeneric(typeof(IDictionary<,>)))
            {
                writer.WriteStartObject();
                foreach (var kvpObj in value.DictionaryKeyValuePairs())
                {
                    var keyValue = kvpObj.Key;
                    var propertyName = (keyValue is IReferenceable)?
                        (keyValue as IReferenceable).id.ToString("N")
                        :
                        (keyValue is IReferenceableOptional)?
                            (keyValue as IReferenceableOptional).HasValue?
                                (keyValue as IReferenceableOptional).id.Value.ToString("N")
                                :
                                "null"
                            :
                            keyValue.ToString();
                    writer.WritePropertyName(propertyName);

                    var valueValue = kvpObj.Value;
                    WriteJson(writer, valueValue, serializer);
                }
                writer.WriteEndObject();
                return;
            }
            if (value is Type)
            {
                var typeValue = (value as Type);
                var stringType = (value as Type).GetClrString();
                writer.WriteValue(stringType);
                return;
            }
            if (value is byte[])
            {
                var typeValue = value as byte[];
                var stringType = typeValue.ToBase64String();
                writer.WriteValue(stringType);
                return;
            }
            if (valueType.IsEnum)
            {
                var stringValue = Enum.GetName(valueType, value);
                writer.WriteValue(stringValue);
                return;
            }
            serializer.Serialize(writer, value);
        }
    }

}
