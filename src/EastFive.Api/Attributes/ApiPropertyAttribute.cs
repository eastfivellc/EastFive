using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using EastFive.Api.Resources;
using EastFive.Extensions;
using EastFive.Linq.Expressions;
using EastFive.Reflection;
using EastFive.Api.Bindings;
using EastFive.Serialization;

namespace EastFive.Api
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class ApiPropertyAttribute : System.Attribute,
        IProvideApiValue, IDocumentProperty,
        IDeserializeFromApiBody<JsonReader>,
        IDeserializeFromRequest<string>,
        ICast<string>
    {
        public ApiPropertyAttribute()
        {
        }

        public string PropertyName { get; set; }

        public string GetPropertyName(MemberInfo member)
        {
            if (this.PropertyName.HasBlackSpace())
                return this.PropertyName;
            return member.GetCustomAttribute<JsonPropertyAttribute, string>(
                (attr) => attr.PropertyName.HasBlackSpace() ? attr.PropertyName : member.Name,
                () => member.Name);
        }

        public StringComparison MatchQualification { get; set; } = StringComparison.Ordinal;

        public TResult Cast<TResult>(object value,
                Type valueType, string path, MemberInfo member,
            Func<string, TResult> onValue,
            Func<TResult> onNoCast)
        {
            if(typeof(Guid).IsAssignableFrom(valueType))
            {
                var gValue = (Guid)value;
                return onValue(gValue.ToString());
            }
            if (typeof(IReferenceable).IsAssignableFrom(valueType))
            {
                var refValue = (IReferenceable)value;
                return onValue(refValue.id.ToString());
            }
            if (typeof(DateTime).IsAssignableFrom(valueType))
            {
                var dtValue = (DateTime)value;
                return onValue(dtValue.ToString());
            }
            if (typeof(string).IsAssignableFrom(valueType))
            {
                var strValue = (string)value;
                return onValue(strValue);
            }

            return onValue(value.ToString());
        }

        public Property GetProperty(MemberInfo member, HttpApplication httpApp)
        {
            string GetDescription()
            {
                return member.GetCustomAttribute<System.ComponentModel.DescriptionAttribute, string>(
                    (attr) => attr.Description,
                    () => string.Empty);
            }
            string GetType()
            {
                var type = member.GetPropertyOrFieldType();
                return Parameter.GetTypeName(type, httpApp);
            }
            var name = GetPropertyName(member);
            var description = GetDescription();
            var options = new KeyValuePair<string, string>[] { };
            return new Property()
            {
                IsIdentfier = member.ContainsAttributeInterface<IIdentifyResource>(),
                IsTitle = member.ContainsAttributeInterface<ITitleResource>(),
                Name = name,
                Description = description,
                Options = options,
                Type = GetType(),
            };
        }

        public virtual bool IsMatch(string propertyKey, ParameterInfo parameterInfo, MemberInfo member,
            IApplication httpApp, IHttpRequest request)
        {
            return PropertyName.Equals(propertyKey, MatchQualification);
        }

        public virtual object UpdateInstance(string propertyKey, JsonReader reader, object instance,
            ParameterInfo parameterInfo, MemberInfo memberInfo,
            IApplication application, IHttpRequest request)
        {
            var objectType = memberInfo.GetPropertyOrFieldType();
            if (objectType.TryGetAttributeInterface(out IDeserializeFromBody<JsonReader> bodyDeserializer))
                return bodyDeserializer.UpdateInstance(propertyKey, reader, instance,
                    parameterInfo, memberInfo, application, request);

            if(objectType.IsArray)
            {
                if(reader.TokenType == JsonToken.StartArray)
                {
                    var elementType = objectType.GetElementType();
                    var arrayValue = ReadArray()
                        .Select(
                            itemReader =>
                            {
                                var elementObj = (application as HttpApplication)
                                    .Bind(reader, elementType,
                                        v => v,
                                        why => objectType.GetDefault());
                                return elementObj;
                            })
                        .CastArray(elementType);
                    return memberInfo.SetPropertyOrFieldValue(instance, arrayValue);
                }
            }

            return (application as HttpApplication).Bind(reader, memberInfo,
                v =>
                {
                    return memberInfo.SetPropertyOrFieldValue(instance, v);
                },
                (why) =>
                {
                    return instance;
                });

            IEnumerable<JsonReader> ReadArray()
            {
                do
                {
                    if (!reader.Read())
                        break;
                    if (reader.TokenType == JsonToken.EndArray)
                        break;

                    yield return reader;
                }
                while (true);
            }
        }

        /// <summary>
        /// String-source flavor of <see cref="UpdateInstance(string, JsonReader, object, ParameterInfo, MemberInfo, IApplication, IHttpRequest)"/>.
        /// Used when the value arrives from a non-body source (query string,
        /// path capture, form scalar). Delegates to <c>httpApp.Bind</c> for
        /// the actual string→target-type conversion so the same binders that
        /// power <c>[QueryParameter]</c> are reused.
        /// </summary>
        public virtual object UpdateInstance(string propertyKey, string raw, object instance,
            ParameterInfo parameterInfo, MemberInfo memberInfo,
            IApplication application, IHttpRequest request)
        {
            return (application as HttpApplication).Bind(raw, memberInfo.GetPropertyOrFieldType(),
                v => memberInfo.SetPropertyOrFieldValue(instance, v),
                why => instance);
        }
    }
}
