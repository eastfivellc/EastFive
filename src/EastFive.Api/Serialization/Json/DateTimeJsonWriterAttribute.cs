using System;
using System.Reflection;
using System.Threading.Tasks;
using EastFive.Reflection;
using Newtonsoft.Json;

namespace EastFive.Api.Serialization.Json
{
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
	public class DateTimeJsonWriterAttribute : Attribute, ICastJsonProperty
	{
        public string DateTimeFormat { get; set; }


		public DateTimeJsonWriterAttribute()
		{
		}

        public bool CanConvert(MemberInfo member, ParameterInfo paramInfo, IHttpRequest httpRequest, IApplication application, IProvideApiValue apiValueProvider, object objectValue)
        {
            var type = member.GetPropertyOrFieldType();
            if (typeof(DateTime).IsAssignableFrom(type))
                return true;

            if (type.TryGetNullableUnderlyingType(out Type nonNullableType))
                if (typeof(DateTime).IsAssignableFrom(nonNullableType))
                    return true;

            return false;
        }

        public async Task WriteAsync(JsonWriter writer, JsonSerializer serializer,
            MemberInfo member, ParameterInfo paramInfo, IProvideApiValue apiValueProvider, object objectValue, object memberValue,
            IHttpRequest httpRequest, IApplication application)
        {
            var dtValueMaybe = (DateTime?)memberValue;
            if(!dtValueMaybe.HasValue)
            {
                await writer.WriteNullAsync();
                return;
            }
            var dtValue = dtValueMaybe.Value;
            if (DateTimeFormat.HasBlackSpace())
            {
                var dateTimeString = dtValue.ToString(this.DateTimeFormat);
                await writer.WriteValueAsync(dateTimeString);
                return;
            }
            
            await writer.WriteValueAsync(dtValueMaybe);
        }
    }
}

