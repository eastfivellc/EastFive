using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using EastFive;
using EastFive.Extensions;
using EastFive.Reflection;
using EastFive.Serialization;

namespace EastFive.Api.Resources
{
    public interface IDocumentParameter
    {
        Parameter GetParameter(ParameterInfo paramInfo, HttpApplication httpApp);
    }

    public struct OpenApiType
    {
        public string type;
        public string format;
        public string contentEncoding;
        public bool array;
    }

    public class Parameter
    {
        public Parameter(ParameterInfo pocoParameter)
        {
            this.PocoParameter = pocoParameter;
        }

        public static string GetTypeName(Type type, HttpApplication httpApp)
        {
            if (type.IsSubClassOfGeneric(typeof(IRef<>)))
                return $"->{GetTypeName(type.GenericTypeArguments.First(), httpApp)}";
            if (type.IsSubClassOfGeneric(typeof(IRefs<>)))
                return $"[]->{GetTypeName(type.GenericTypeArguments.First(), httpApp)}";
            if (type.IsSubClassOfGeneric(typeof(IRefOptional<>)))
                return $"->{GetTypeName(type.GenericTypeArguments.First(), httpApp)}?";
            if (type.IsArray)
                return $"{GetTypeName(type.GetElementType(), httpApp)}[]";
            return type.IsNullable(
                nullableBase => $"{GetTypeName(nullableBase, httpApp)}?",
                () => httpApp.GetResourceName(type));
        }

        public static OpenApiType GetOpenApiTypeName(Type type, HttpApplication httpApp)
        {
            if (type.IsSubClassOfGeneric(typeof(IRef<>)))
                return new OpenApiType() { type = "string", format = "uuid" };
            if (type.IsSubClassOfGeneric(typeof(IRefs<>)))
                return new OpenApiType() { array = true, type = "string", format = "uuid" };
            if (type.IsSubClassOfGeneric(typeof(IRefOptional<>)))
                return new OpenApiType() { type = "string", format = "uuid" };
            if (type.IsSubClassOfGeneric(typeof(IRefOptional<>)))
                return new OpenApiType() { type = "string", format = "uuid" };
            if (type == typeof(Guid))
                return new OpenApiType() { type = "string", format = "uuid" };
            if (type == typeof(int))
                return new OpenApiType() { type = "integer", format = "int32" };
            if (type == typeof(long))
                return new OpenApiType() { type = "integer", format = "int64" };
            if (type == typeof(bool))
                return new OpenApiType() { type = "boolean" };
            if (type == typeof(Uri))
                return new OpenApiType() { type = "string", format = "uri" };
            if (type == typeof(DateTime))
                return new OpenApiType() { type = "string", format = "date-time" };
            if (type == typeof(TimeSpan))
                return new OpenApiType() { type = "string", format = "duration" };
            if (type.IsNumeric())
                return new OpenApiType() { type = "number" };
            if (type == typeof(string))
                return new OpenApiType() { type = "string" };
            if (type == typeof(byte[]))
                return new OpenApiType() { type = "string", contentEncoding = "base64" };
            if (type == typeof(Func<Task<byte[]>>))
                return new OpenApiType() { type = "string", contentEncoding = "base64" };
            if (type.IsArray)
            {
                var openApiType = GetOpenApiTypeName(type.GetElementType(), httpApp);
                openApiType.array = true;
                return openApiType;
            }
            return type.IsNullable(
                nullableBase => GetOpenApiTypeName(nullableBase, httpApp),
                () => new OpenApiType { type = "object" });
        }

        public string Name { get; set; }
        public string Description { get; set; }
        public bool Required { get; set; }
        public bool Default { get; set; }
        public string Where { get; set; }
        public string Type { get; set; }
        public ParameterInfo PocoParameter { get; set; }
        public OpenApiType OpenApiType { get; set; }
    }
}
