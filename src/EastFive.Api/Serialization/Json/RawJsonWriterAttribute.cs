using System;
using System.Reflection;
using System.Threading.Tasks;
using EastFive.Reflection;
using Newtonsoft.Json;

namespace EastFive.Api.Serialization.Json
{
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
	public class RawJsonWriterAttribute : Attribute, ICastJsonProperty
	{
		public RawJsonWriterAttribute()
		{
		}

        public bool CanConvert(MemberInfo member, ParameterInfo paramInfo, IHttpRequest httpRequest, IApplication application, IProvideApiValue apiValueProvider, object objectValue)
        {
            var type = member.GetPropertyOrFieldType();
            if (typeof(string).IsAssignableFrom(type))
                return true;

            return false;
        }

        public async Task WriteAsync(JsonWriter writer, JsonSerializer serializer,
            MemberInfo member, ParameterInfo paramInfo, IProvideApiValue apiValueProvider, object objectValue, object memberValue,
            IHttpRequest httpRequest, IApplication application)
        {
            var strValue = (string)memberValue;
            await writer.WriteRawValueAsync(strValue);
        }
    }
}
