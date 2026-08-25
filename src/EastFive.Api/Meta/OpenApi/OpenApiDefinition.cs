using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EastFive.Extensions;
using EastFive.Linq;
using System.IO;
using EastFive.Api.Auth;

namespace EastFive.Api.Meta.OpenApi
{
    [FunctionViewController(
        Route = "OpenApiSchema",
        Namespace = "meta")]
    [OpenApiRoute(Collection = "EastFive.Api.Meta")]
    public class OpenApiSchema : IReferenceable
    {
        #region Properties

        [JsonIgnore]
        public Guid id => this.openApiSchemaRef.id;

        public const string IdPropertyName = "id";
        [JsonProperty(PropertyName = IdPropertyName)]
        [ApiProperty(PropertyName = IdPropertyName)]
        public IRef<OpenApiSchema> openApiSchemaRef;

        public string openapi => "3.0.0";

        public Info info;

        public Server[] servers { get; set; }
        public Path[] paths { get; set; }
        public Components components { get; set; }

        #endregion

        [EastFive.Api.HttpGet]
        [RequiredClaim(
            System.Security.Claims.ClaimTypes.Role,
            ClaimValues.Roles.SuperAdmin)]
        public static IHttpResponse GetSchema(
                [OptionalQueryParameter]string collections,
                IInvokeApplication invokeApplication,
                HttpApplication httpApp, IHttpRequest request, IProvideUrl url,
            TextResponse onSuccess)
        {
            var lookups = httpApp
                .GetResources()
                .Where(
                    resource =>
                    {
                        if (collections.IsNullOrWhiteSpace())
                            return true;
                        var collection = resource.TryGetAttributeInterface(out IDocumentOpenApiRoute documentOpenApiRoute) ?
                            documentOpenApiRoute.Collection
                            :
                            resource.Namespace;
                        return collection.StartsWith(collections, StringComparison.OrdinalIgnoreCase);
                    })
                .ToArray();
            var manifest = new EastFive.Api.Resources.Manifest(lookups, httpApp);

            var sb = new StringBuilder();
            var sw = new StringWriter(sb);
            var jsonSerializer = new JsonSerializer();

            var server = new Server
            {
                url = invokeApplication.ServerLocation.AbsoluteUri.TrimEnd('/'),
            };
            var info = new Info
            {
                license = new License
                {
                    name = "Private"
                },
                title = $"{httpApp.GetType().Namespace}",
                version = "1.2.3",
            };

            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                writer.Formatting = Formatting.Indented;

                writer.WriteStartObject();
                writer.WritePropertyName("openapi");
                writer.WriteValue("3.0.0");
                writer.WritePropertyName("info");
                jsonSerializer.Serialize(writer, info);
                writer.WritePropertyName("servers");
                jsonSerializer.Serialize(writer, server.AsArray());
                writer.WritePropertyName("paths");
                writer.WriteStartObject();

                var schemas = new Dictionary<string, Resources.Parameter[]>();

                foreach(var route in manifest.Routes)
                {
                    foreach(var methodPathGrp in route.Methods.GroupBy(method => method.Path.OriginalString))
                    {
                        writer.WritePropertyName(methodPathGrp.Key);
                        writer.WriteStartObject();
                        foreach(var actionGrp in methodPathGrp.GroupBy(method => method.HttpMethod))
                        {
                            var action = actionGrp.First();
                            var responses = action.Responses
                                .Where(response => !response.StatusCode.IsDefault())
                                .GroupBy(response => response.StatusCode);
                            if (!responses.Any())
                                continue;
                            writer.WritePropertyName(actionGrp.Key.ToLower());
                            writer.WriteStartObject();
                            writer.WritePropertyName("description");
                            writer.WriteValue(action.Description);
                            writer.WritePropertyName("operationId");
                            writer.WriteValue(action.Name);
                            var queryParams = action.Parameters
                                .Where(p => "QUERY".Equals(p.Where, StringComparison.OrdinalIgnoreCase))
                                .ToArray();
                            if (queryParams.Any())
                            {
                                writer.WritePropertyName("parameters");
                                writer.WriteStartArray();
                                foreach (var parameter in queryParams)
                                {
                                    writer.WriteStartObject();
                                    writer.WritePropertyName("name");
                                    writer.WriteValue(parameter.Name);
                                    writer.WritePropertyName("in");
                                    writer.WriteValue(parameter.Where.ToLower());
                                    if (parameter.Description.HasBlackSpace())
                                    {
                                        writer.WritePropertyName("description");
                                        writer.WriteValue(parameter.Description);
                                    }
                                    writer.WritePropertyName("required");
                                    writer.WriteValue(parameter.Required);
                                    //writer.WritePropertyName("style");
                                    //writer.WriteValue("form");
                                    writer.WritePropertyName("schema");
                                    writer.WriteStartObject();
                                    if (parameter.OpenApiType.array)
                                    {
                                        writer.WritePropertyName("type");
                                        writer.WriteValue("array");
                                        writer.WritePropertyName("items");
                                        writer.WriteStartObject();
                                        writer.WritePropertyName("type");
                                        writer.WriteValue(parameter.OpenApiType.type);
                                        writer.WriteEndObject();
                                    }
                                    else
                                    {
                                        writer.WritePropertyName("type");
                                        writer.WriteValue(parameter.OpenApiType.type);
                                        if (parameter.OpenApiType.format.HasBlackSpace())
                                        {
                                            writer.WritePropertyName("format");
                                            writer.WriteValue(parameter.OpenApiType.format);
                                        }
                                        if (parameter.OpenApiType.contentEncoding.HasBlackSpace())
                                        {
                                            writer.WritePropertyName("contentEncoding");
                                            writer.WriteValue(parameter.OpenApiType.contentEncoding);
                                        }
                                    }
                                    writer.WriteEndObject();
                                    writer.WriteEndObject();
                                }
                                writer.WriteEndArray();
                            }

                            var formParams = action.Parameters
                                .Where(p => "BODY".Equals(p.Where, StringComparison.OrdinalIgnoreCase))
                                .ToArray();
                            if(formParams.Any())
                            {
                                var key = $"{route.Name}{action.Name}";
                                writer.WritePropertyName("requestBody");
                                writer.WriteStartObject();
                                writer.WritePropertyName("content");
                                writer.WriteStartObject();
                                writer.WritePropertyName("application/json");
                                writer.WriteStartObject();
                                writer.WritePropertyName("schema");
                                writer.WriteStartObject();
                                writer.WritePropertyName("$ref");
                                writer.WriteValue($"#/components/schemas/{key}");
                                writer.WriteEndObject();
                                writer.WriteEndObject();
                                writer.WriteEndObject();
                                writer.WriteEndObject();
                                schemas.TryAdd(key, formParams);
                            }

                            writer.WritePropertyName("responses");
                            writer.WriteStartObject();
                            foreach(var responseGrp in responses)
                            {
                                writer.WritePropertyName($"{(int)responseGrp.Key}");
                                writer.WriteStartObject();
                                writer.WritePropertyName("description");
                                var description = responseGrp.Select(response => response.Name).Join(" or ");
                                writer.WriteValue(description);
                                writer.WriteEndObject();
                            }
                            writer.WriteEndObject();

                            writer.WriteEndObject();
                        }
                        writer.WriteEndObject();
                    }
                }

                writer.WriteEndObject();

                writer.WritePropertyName("components");
                writer.WriteStartObject();
                writer.WritePropertyName("schemas");
                writer.WriteStartObject();
                foreach(var schema in schemas)
                {
                    writer.WritePropertyName(schema.Key);
                    writer.WriteStartObject();
                    writer.WritePropertyName("type");
                    writer.WriteValue("object");
                    var properties = schema.Value
                        .Distinct(s => s.Name)
                        .ToArray();
                    var requiredArray = properties
                        .Where(s => s.Required)
                        .Select(s => s.Name)
                        .ToArray();
                    if (requiredArray.Any())
                    {
                        writer.WritePropertyName("required");
                        jsonSerializer.Serialize(writer, requiredArray);
                    }
                    writer.WritePropertyName("properties");
                    writer.WriteStartObject();
                    foreach(var prop in properties)
                    {
                        writer.WritePropertyName(prop.Name);
                        writer.WriteStartObject();
                        writer.WritePropertyName("type");
                        writer.WriteValue(prop.OpenApiType.type);
                        if (prop.OpenApiType.format.HasBlackSpace())
                        {
                            writer.WritePropertyName("format");
                            writer.WriteValue(prop.OpenApiType.format);
                        }
                        if (prop.OpenApiType.contentEncoding.HasBlackSpace())
                        {
                            writer.WritePropertyName("contentEncoding");
                            writer.WriteValue(prop.OpenApiType.contentEncoding);
                        }
                        writer.WriteEndObject();
                    }
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                }
                writer.WriteEndObject();
                writer.WriteEndObject();

                writer.WriteEndObject();
            }

            var json = sb.ToString();
            return onSuccess(json, contentType: "application/json");
        }
    }

    public class Info
    {
        public string version { get; set; }
        public string title { get; set; }
        public License license { get; set; }
    }

    public class License
    {
        public string name { get; set; }
    }

    public class Path
    {
        public string name;

        public System.Net.Http.HttpMethod method;

        public string summary { get; set; }
        public string operationId { get; set; }
        public string[] tags { get; set; }
        public Parameter[] parameters { get; set; }
        public Response[] responses { get; set; }
    }

    public class Schema
    {
        public string type { get; set; }
        public string[] required { get; set; }
        public Properties properties { get; set; }
    }

    public class Response
    {
        public System.Net.HttpStatusCode? code;
        public string description;
        public Header[] headers;
        public Content content;
    }

    public class Header
    {
        public string name;
        public string description;
        public HeaderSchema schema;
    }

    public class HeaderSchema
    {
        public string type { get; set; }
        public string format { get; set; }
    }

    public class Content
    {
        public ApplicationJson applicationjson { get; set; }
    }

    public class ApplicationJson
    {
        public JsonSchema schema { get; set; }
    }

    public class JsonSchema
    {
        public string _ref { get; set; }
    }

    public class Parameter
    {
        public string name { get; set; }
        public string _in { get; set; }
        public string description { get; set; }
        public bool required { get; set; }
        public HeaderSchema schema { get; set; }
    }

    public class Components
    {
        public ComponentSchema[] schemas;
    }

    public class ComponentSchema
    {
        public Error Error { get; set; }
    }

    public class Properties
    {
        public Id id { get; set; }
        public Name name { get; set; }
        public Tag tag { get; set; }
    }

    public class Id
    {
        public string type { get; set; }
        public string format { get; set; }
    }

    public class Name
    {
        public string type { get; set; }
    }

    public class Tag
    {
        public string type { get; set; }
    }

    public class Error
    {
        public string type { get; set; }
        public string[] required { get; set; }
        public Property[] properties { get; set; }
    }

    public class Property
    {
        public Code code { get; set; }
        public Message message { get; set; }
    }

    public class Code
    {
        public string type { get; set; }
        public string format { get; set; }
    }

    public class Message
    {
        public string type { get; set; }
    }

    public class Server
    {
        public string url { get; set; }
    }

}
