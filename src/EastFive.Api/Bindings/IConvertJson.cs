using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public interface IConvertJson
    {
        bool CanConvert(Type objectType, IHttpRequest httpRequest, IApplication application);
        object Read(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer,
            IHttpRequest httpRequest, IApplication application);
        void Write(JsonWriter writer, object value, JsonSerializer serializer,
            IHttpRequest httpRequest, IApplication application);
    }

    public interface ICastJson
    {
        bool CanConvert(Type type, object value,
            IHttpRequest httpRequest, IApplication application);

        Task WriteAsync(JsonWriter writer, JsonSerializer serializer,
            Type type, object value,
            IHttpRequest httpRequest, IApplication application);
    }

    public interface ICastJsonProperty
    {
        bool CanConvert(MemberInfo member, ParameterInfo paramInfo,
            IHttpRequest httpRequest, IApplication application,
            IProvideApiValue apiValueProvider, object objectValue);

        Task WriteAsync(JsonWriter writer, JsonSerializer serializer,
            MemberInfo member, ParameterInfo paramInfo,
            IProvideApiValue apiValueProvider, object objectValue, object memberValue,
            IHttpRequest httpRequest, IApplication application);
    }
}
