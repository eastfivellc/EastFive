using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.Data;

using Newtonsoft.Json;

using EastFive;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Reflection;
using EastFive.Serialization;
using System.Runtime.CompilerServices;

namespace EastFive.Api.Serialization.Json
{
    public class CastSerializationAttribute : Attribute, IProvideSerialization
    {
        public CastSerializationAttribute()
        {
        }

        public string MediaType => "application/json";

        public string ContentType { get; set; }

        private const double defaultPreference = -111;

        public double Preference { get; set; } = defaultPreference;

        public bool IgnoreNull { get; set; } = true;

        public double GetPreference(IHttpRequest request)
        {
            if (Preference != defaultPreference)
                return Preference;

            return 0.1;
        }

        public async Task SerializeAsync(Stream responseStream,
            IApplication httpApp, IHttpRequest request,
            ParameterInfo paramInfo, object obj)
        {
            var jsonCastersHttpApp = httpApp
                .GetType()
                .GetAttributesInterface<ICastJson>()
                .ToArray();

            var encoding = request.TryGetAcceptCharset(out Encoding writerEncoding) ?
                writerEncoding
                :
                new UTF8Encoding(false);

            using (var textWriter = new StreamWriter(responseStream, encoding))
            {
                var jsonWriter = new JsonTextWriter(textWriter);

                var serializer = new JsonSerializer();
                var converter = new Serialization.ExtrudeConvert(request, httpApp);
                serializer.Converters.Add(converter);

                if (obj.IsNotDefaultOrNull())
                {
                    if (obj.GetType().IsArray)
                    {
                        await jsonWriter.WriteStartArrayAsync();
                        foreach (object item in (Array)obj)
                        {
                            await WriteObjectToStream(jsonWriter, serializer, item);
                        }
                        await jsonWriter.WriteEndArrayAsync();
                        return;
                    }
                }

                await WriteObjectToStream(jsonWriter, serializer, obj);
            }

            async Task WriteObjectToStream(JsonTextWriter jsonWriter, JsonSerializer serializer, object obj)
            {
                await jsonWriter.WriteStartObjectAsync();

                var members = obj.GetType()
                    .GetPropertyAndFieldsWithAttributesInterface<IProvideApiValue>();

                foreach (var (member, apiValueProvider) in members)
                {
                    var memberValue = member.GetPropertyOrFieldValue(obj);
                    var isNull = memberValue.IsNull();
                    if (isNull)
                    {
                        if(IgnoreNull)
                            continue;
                    }

                    var propertyName = apiValueProvider.GetPropertyName(member);
                    await jsonWriter.WritePropertyNameAsync(propertyName);

                    await WriteAsync(member.GetAttributesInterface<ICastJsonProperty>(),
                        onCouldNotCast: () =>
                        {
                            if(isNull)
                                return jsonWriter.WriteNullAsync();

                            var memberType = memberValue.GetType();
                            return WriteAsync(memberType.GetAttributesInterface<ICastJsonProperty>(),
                                () =>
                                {
                                    var memberType = member.GetPropertyOrFieldType();
                                    return WriteAsync(memberType.GetAttributesInterface<ICastJsonProperty>(),
                                       async () =>
                                       {
                                           await WriteValueToStreamAsync(jsonWriter, serializer, memberValue, member.GetPropertyOrFieldType());
                                       });
                                });
                        });

                    Task WriteAsync(IEnumerable<ICastJsonProperty> castAttrs,
                        Func<Task> onCouldNotCast)
                    {
                        return castAttrs
                            .Where(attr => attr.CanConvert(member, paramInfo,
                                                httpRequest: request, application: httpApp,
                                                apiValueProvider: apiValueProvider, objectValue: obj))
                            .First(
                                async (jsonCaster, next) =>
                                {
                                    //var memberValue = member.GetPropertyOrFieldValue(obj);
                                    var isNull = memberValue.IsNull();
                                    if (isNull)
                                        if (IgnoreNull)
                                            return;

                                    await jsonCaster.WriteAsync(jsonWriter, serializer, member, paramInfo,
                                        apiValueProvider, obj, memberValue, request, httpApp);
                                },
                                () => onCouldNotCast());
                    }
                }

                await jsonWriter.WriteEndObjectAsync();
            }

            async Task WriteValueToStreamAsync(JsonTextWriter jsonWriter, JsonSerializer serializer, object obj,
                Type typeToSerialize = default)
            {
                if (obj.IsNull())
                {
                    await jsonWriter.WriteNullAsync();
                    return;
                }

                if (typeToSerialize.IsDefaultOrNull())
                    typeToSerialize = obj.GetType();

                if (typeToSerialize.TryGetAttributeInterface(out IProvideSerialization serializationProvider))
                {
                    using (var cacheStream = new MemoryStream())
                    {
                        await serializationProvider.SerializeAsync(cacheStream,
                                httpApp, request, paramInfo, obj);
                        var rawJson = cacheStream.ToArray().GetString(encoding);
                        await jsonWriter.WriteRawValueAsync(rawJson);
                    }
                    return;
                }

                if (typeToSerialize.TryGetAttributeInterface(out ICastJson jsonProvider))
                {
                    if(jsonProvider.CanConvert(typeToSerialize, obj, request, httpApp))
                    {
                        await jsonProvider.WriteAsync(jsonWriter, serializer, typeToSerialize, obj, request, httpApp);
                        return;
                    }
                }

                if (typeToSerialize.IsTuple(obj, out var tupleProperties))
                {
                    await jsonWriter.WriteStartObjectAsync();
                    foreach(var tupleProperty in tupleProperties)
                    {
                        await jsonWriter.WritePropertyNameAsync(tupleProperty.name);
                        await jsonWriter.WriteValueAsync(tupleProperty.value);
                    }
                    await jsonWriter.WriteEndObjectAsync();
                    return;
                }

                if (typeToSerialize.IsArray)
                {
                    await WriteArrayAsync(typeToSerialize);
                    return;
                }

                if (typeToSerialize.IsSubClassOfGeneric(typeof(IDictionary<,>)))
                {
                    await WriteDictionaryAsync(typeToSerialize);
                    return;
                }

                await jsonCastersHttpApp
                    .Where(attr => attr.CanConvert(typeToSerialize, obj,
                        httpRequest: request, application: httpApp))
                    .First(
                        async (jsonCaster, next) =>
                        {
                            //var memberValue = member.GetPropertyOrFieldValue(obj);
                            var isNull = obj.IsNull();
                            if (isNull)
                                if (IgnoreNull)
                                    return;

                            await jsonCaster.WriteAsync(jsonWriter, serializer,
                                typeToSerialize, obj, request, httpApp);
                        },
                        async () =>
                        {
                            await typeToSerialize.IsNullable(
                                async nullFixedType =>
                                {
                                    if (obj == null)
                                    {
                                        await jsonWriter.WriteNullAsync();
                                        return;
                                    }

                                    await WriteValueToStreamAsync(jsonWriter, serializer, obj, nullFixedType);
                                },
                                async () =>
                                {
                                    await jsonWriter.WriteNullAsync();
                                    await jsonWriter.WriteCommentAsync(
                                        $"Cannot find {nameof(IProvideSerialization)} interface for " +
                                        $"{typeToSerialize.FullName}");
                                });
                            });

                async Task WriteArrayAsync(Type memberType)
                {
                    if (obj.IsNull())
                    {
                        await jsonWriter.WriteNullAsync();
                        return;
                    }
                    var enumerable = (IEnumerable)obj;
                    await jsonWriter.WriteStartArrayAsync();

                    if (!enumerable.IsNull())
                    {
                        foreach (var item in enumerable)
                        {
                            await WriteValueToStreamAsync(jsonWriter, serializer, item);
                        }
                    }
                    await jsonWriter.WriteEndArrayAsync();
                }

                async Task WriteDictionaryAsync(Type memberType)
                {
                    if (obj.IsNull())
                    {
                        await jsonWriter.WriteNullAsync();
                        return;
                    }
                    var enumerable = obj.DictionaryKeyValuePairs();
                    await jsonWriter.WriteStartObjectAsync();

                    if (!enumerable.IsNull())
                    {
                        foreach (var item in enumerable)
                        {
                            var keyValue = GetKeyValueString(item.Key);
                            await jsonWriter.WritePropertyNameAsync(keyValue);
                            await WriteValueToStreamAsync(jsonWriter, serializer, item.Value);
                        }
                    }
                    await jsonWriter.WriteEndObjectAsync();

                    string GetKeyValueString(object keyValue)
                    {
                        if(keyValue.IsNull())
                        {
                            return "";
                        }
                        if(typeof(DateTime).IsAssignableFrom(keyValue.GetType()))
                        {
                            var dtValue = (DateTime)keyValue;
                            return dtValue.ToString("s", System.Globalization.CultureInfo.InvariantCulture);
                        }

                        return keyValue.ToString();
                    }
                }
            }
        }
    }
}

