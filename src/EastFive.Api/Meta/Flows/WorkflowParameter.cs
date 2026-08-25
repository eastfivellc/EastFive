using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using EastFive;
using EastFive.Extensions;
using EastFive.Api.Meta.Postman.Resources.Collection;
using EastFive.Api.Resources;
using EastFive.Linq;
using EastFive.Web.Configuration;


namespace EastFive.Api.Meta.Flows
{
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = true)]
    public abstract class WorkflowParameterBaseAttribute : System.Attribute,
        IDefineWorkflowRequestProperty,
        IDefineQueryItem,
        IDefineWorkflowRequestPropertyFormData
    {
        public string Scope { get; set; }

        public string Description { get; set; }

        public bool Disabled { get; set; } = false;

        /// <summary>
        /// Explicit body field name. When set, this attribute fully describes its own field and is
        /// emitted into the request body regardless of any co-located binding attribute — which lets
        /// several workflow attributes document a single whole-body parameter. When unset, the field
        /// name and location are derived from the co-located binding attribute (legacy behavior).
        /// </summary>
        public string Name { get; set; }

        protected abstract string GetValue(ParameterInfo parameter, out bool quoted);

        protected virtual string GetDescription(ParameterInfo parameter)
        {
            if (Description.HasBlackSpace())
                return this.Description;

            if (!parameter.ContainsCustomAttribute<System.ComponentModel.DescriptionAttribute>())
                return default;

            var descrAttr = parameter.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
            return descrAttr.Description;
        }

        protected virtual bool IsFileType(ParameterInfo parameter) => false;

        protected enum RequestPropertyLocation
        {
            Body,
            FormData,
            Query,
        }

        protected readonly struct RequestPropertyRender
        {
            public RequestPropertyRender(string propertyName, string value, bool quoted,
                string description, bool isFileType)
            {
                this.PropertyName = propertyName;
                this.Value = value;
                this.Quoted = quoted;
                this.Description = description;
                this.IsFileType = isFileType;
            }

            public string PropertyName { get; }
            public string Value { get; }
            public bool Quoted { get; }
            public string Description { get; }
            public bool IsFileType { get; }
        }

        /// <summary>
        /// Single routing seam: resolves the property name + value once, decides where the property
        /// belongs (body / form-data / query), then dispatches to the matching handler. Each public
        /// emit method injects only the output it owns.
        /// </summary>
        protected TResult RouteRequestProperty<TResult>(ParameterInfo parameter,
            Func<RequestPropertyRender, TResult> onBody,
            Func<RequestPropertyRender, TResult> onFormData,
            Func<RequestPropertyRender, TResult> onQuery)
        {
            var location = GetLocation(parameter);
            var value = GetValue(parameter, out bool quoted);
            var render = new RequestPropertyRender(
                propertyName: GetPropertyName(parameter),
                value: value,
                quoted: quoted,
                description: GetDescription(parameter),
                isFileType: IsFileType(parameter));
            return location switch
            {
                RequestPropertyLocation.Body => onBody(render),
                RequestPropertyLocation.FormData => onFormData(render),
                _ => onQuery(render),
            };
        }

        private string GetPropertyName(ParameterInfo parameter)
        {
            if (Name.HasBlackSpace())
                return Name;
            return parameter.TryGetAttributeInterface(out IBindApiValue apiBinder) ?
                apiBinder.GetKey(parameter)
                :
                parameter.Name;
        }

        private RequestPropertyLocation GetLocation(ParameterInfo parameter)
        {
            if (Name.HasBlackSpace())
                return RequestPropertyLocation.Body;
            if (parameter.ContainsAttributeInterface<IBindJsonApiValue>(inherit: true))
                return RequestPropertyLocation.Body;
            if (parameter.ContainsAttributeInterface<IBindFormDataApiValue>(inherit: true))
                return RequestPropertyLocation.FormData;
            if (parameter.ContainsAttributeInterface<IBindMultipartApiValue>(inherit: true))
                return RequestPropertyLocation.FormData;
            return RequestPropertyLocation.Query;
        }

        public void AddProperties(JsonWriter requestObj, ParameterInfo parameter)
        {
            RouteRequestProperty(parameter,
                onBody: render =>
                {
                    requestObj.WritePropertyName(render.PropertyName);
                    if (render.Quoted)
                        requestObj.WriteValue(render.Value);
                    else
                        requestObj.WriteRawValue(render.Value);
                    if (render.Description.HasBlackSpace())
                        requestObj.WriteComment(render.Description);
                    return true;
                },
                onFormData: _ => false,
                onQuery: _ => false);
        }

        public FormData[] GetFormData(ParameterInfo parameter)
        {
            return RouteRequestProperty(parameter,
                onBody: _ => new FormData[] { },
                onFormData: render => new FormData[]
                {
                    new FormData
                    {
                        key = render.PropertyName,
                        value = render.Value,
                        type = render.IsFileType ? "file" : "text",
                        description = render.Description,
                        disabled = this.Disabled,
                    }
                },
                onQuery: _ => new FormData[] { });
        }

        public QueryItem[] GetQueryItem(Method method, ParameterInfo parameter)
        {
            return RouteRequestProperty(parameter,
                onBody: _ => default(QueryItem[]),
                onFormData: _ => default(QueryItem[]),
                onQuery: render => new QueryItem
                {
                    key = render.PropertyName,
                    value = render.Value,
                    description = render.Description,
                    disabled = this.Disabled,
                }.AsArray());
        }

        public QueryItem[] GetQueryItems(Method method)
        {
            return new QueryItem[] { };
        }
    }

    public interface IDefineWorkflowParameterAttributes
    {
        bool IsFileType(ParameterInfo parameter);
    }

    public class WorkflowParameterAttribute : WorkflowParameterBaseAttribute
    {
        public string Value { get; set; }

        public bool Quoted { get; set; } = true;

        protected override string GetValue(ParameterInfo parameter, out bool quoted)
        {
            quoted = this.Quoted;
            return this.Value;
        }

        protected override bool IsFileType(ParameterInfo parameter)
        {
            if (parameter.ParameterType == typeof(System.IO.Stream))
                return true;
            if(parameter.ParameterType.IsSubClassOfGeneric(typeof(System.IO.Stream)))
                return true;

            if (!parameter.ParameterType.TryGetAttributeInterface(
                out IDefineWorkflowParameterAttributes defineWorkflowParameterAttributes))
                return false;

            return defineWorkflowParameterAttributes.IsFileType(parameter);
        }
    }

    public class WorkflowNewIdAttribute : WorkflowParameterBaseAttribute
    {
        protected override string GetValue(ParameterInfo parameter, out bool quoted)
        {
            quoted = true;
            return "{{$guid}}";
        }

        protected override string GetDescription(ParameterInfo parameter)
        {
            var desc = base.GetDescription(parameter);
            if (desc.HasBlackSpace())
                return desc;

            if (!parameter.ParameterType.GenericTypeArguments.Any())
                return null;

            var refName = parameter.ParameterType.GenericTypeArguments.First().FullName;
            return $"ID of a {refName}";
        }

        
    }

    public class WorkflowEnumAttribute : WorkflowParameterBaseAttribute
    {
        public string Value { get; set; }

        private string GetOptions(ParameterInfo parameter)
        {
            if (!parameter.ParameterType.IsEnum)
                return $"WARINING {parameter.Member.DeclaringType.FullName}..{parameter.Member.Name}({parameter.Name}) is tagged as Enum workflow but is not an Enum.";

            return Enum.GetNames(parameter.ParameterType)
                .Join(',');
        }

        override protected string GetValue(ParameterInfo parameter, out bool quoted)
        {
            quoted = true;
            if (!parameter.ParameterType.IsEnum)
                return $"WARINING {parameter.Member.DeclaringType.FullName}..{parameter.Member.Name}({parameter.Name}) is tagged as Enum workflow but is not an Enum.";

            if (this.Value.IsNullOrWhiteSpace())
                return null;

            return Enum.GetNames(parameter.ParameterType)
                .Where(name => name == this.Value)
                .First(
                    (name, next) => name,
                    () => $"WARNING:`{this.Value}` is not a valid value.");
        }

        protected override string GetDescription(ParameterInfo parameter)
        {
            var desc = base.GetDescription(parameter);
            var options = GetOptions(parameter);
            if (desc.HasBlackSpace())
                return $"{desc} Select one of [{options}]";
            return options;
        }
    }

    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = true)]
    public class WorkflowArrayObjectParameterAttribute : System.Attribute,
        IDefineWorkflowRequestProperty
    {
        public string Scope { get; set; }

        /// <summary>
        /// Explicit body field name; see <see cref="WorkflowParameterBaseAttribute.Name"/>. When set,
        /// this attribute supplies its own field name so it can sit on a whole-body parameter.
        /// </summary>
        public string Name { get; set; }

        public string Value0 { get; set; }
        public string Value1 { get; set; }
        public string Value2 { get; set; }
        public string Value3 { get; set; }

        public void AddProperties(JsonWriter requestObj, ParameterInfo parameter)
        {
            var propertyName = Name.HasBlackSpace()
                ? Name
                : parameter.TryGetAttributeInterface(out IBindApiValue apiBinder)
                    ? apiBinder.GetKey(parameter)
                    : parameter.Name;
            requestObj.WritePropertyName(propertyName);
            requestObj.WriteStartArray();
            if (Value0.HasBlackSpace())
            {
                requestObj.WriteValue(Value0);
            }
            if (Value1.HasBlackSpace())
            {
                requestObj.WriteValue(Value1);
            }
            if (Value2.HasBlackSpace())
            {
                requestObj.WriteValue(Value2);
            }
            if (Value3.HasBlackSpace())
            {
                requestObj.WriteValue(Value3);
            }
            requestObj.WriteEndArray();
        }
    }

    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = true)]
    public class WorkflowObjectParameterAttribute : System.Attribute,
        IDefineWorkflowRequestProperty
    {
        public string Scope { get; set; }

        /// <summary>
        /// Explicit body field name; see <see cref="WorkflowParameterBaseAttribute.Name"/>. When set,
        /// this attribute supplies its own field name so it can sit on a whole-body parameter.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Force array-of-objects mode. Required when the attribute sits on a whole-body parameter
        /// (where the decorated parameter type can no longer be inspected to infer the shape); the
        /// inner field names then come from <see cref="ItemNameKey"/>/<see cref="ItemValueKey"/>.
        /// </summary>
        public bool AsArray { get; set; }

        /// <summary>
        /// Inner field name used for each <c>Key*</c> when the decorated parameter is a
        /// collection (array / <see cref="IEnumerable{T}"/>) and the element type does not
        /// expose <see cref="JsonPropertyAttribute"/> members. When the element type does
        /// expose them, its first two property names are used instead (e.g. a
        /// <c>{name, value}</c> struct yields <c>name</c>/<c>value</c>). Ignored in the
        /// default flat-object mode.
        /// </summary>
        public string ItemNameKey { get; set; } = "key";

        /// <summary>
        /// Inner field name used for each <c>Value*</c> in collection mode; see
        /// <see cref="ItemNameKey"/>.
        /// </summary>
        public string ItemValueKey { get; set; } = "value";

        public string Key0 { get; set; }
        public string AppSettingKey0 { get; set; }
        public string Value0 { get; set; }
        public string AppSettingValue0 { get; set; }

        public string Key1 { get; set; }
        public string AppSettingKey1 { get; set; }
        public string Value1 { get; set; }
        public string AppSettingValue1 { get; set; }

        public string Key2 { get; set; }
        public string AppSettingKey2 { get; set; }
        public string Value2 { get; set; }
        public string AppSettingValue2 { get; set; }

        public string Key3 { get; set; }
        public string AppSettingKey3 { get; set; }
        public string Value3 { get; set; }
        public string AppSettingValue3 { get; set; }


        public void AddProperties(JsonWriter requestObj, ParameterInfo parameter)
        {
            var propertyName = Name.HasBlackSpace()
                ? Name
                : parameter.TryGetAttributeInterface(out IBindApiValue apiBinder)
                    ? apiBinder.GetKey(parameter)
                    : parameter.Name;
            requestObj.WritePropertyName(propertyName);

            var pairs = new[]
            {
                (Key0, AppSettingKey0, Value0, AppSettingValue0),
                (Key1, AppSettingKey1, Value1, AppSettingValue1),
                (Key2, AppSettingKey2, Value2, AppSettingValue2),
                (Key3, AppSettingKey3, Value3, AppSettingValue3),
            };

            // When the decorated parameter is a collection (or AsArray is set, e.g. on a whole-body
            // parameter whose element type can't be inspected), emit an array of
            // { <nameKey>: key, <valueKey>: value } objects so shapes like PropertyValue[]
            // ({name, value}) or PropertySchema[] ({name, type}) round-trip. Otherwise keep
            // the flat key/value object the attribute has always produced.
            var arrayMode = TryGetArrayItemKeys(parameter, out var nameKey, out var valueKey) || AsArray;
            if (arrayMode)
            {
                requestObj.WriteStartArray();
                foreach (var (key, appSettingKey, value, appSettingValue) in pairs)
                {
                    if (!TryResolveKey(key, appSettingKey, out var resolvedKey))
                        continue;
                    requestObj.WriteStartObject();
                    requestObj.WritePropertyName(nameKey);
                    requestObj.WriteValue(resolvedKey);
                    requestObj.WritePropertyName(valueKey);
                    WriteResolvedValue(requestObj, value, appSettingValue);
                    requestObj.WriteEndObject();
                }
                requestObj.WriteEndArray();
                return;
            }

            requestObj.WriteStartObject();
            foreach (var (key, appSettingKey, value, appSettingValue) in pairs)
            {
                if (!TryResolveKey(key, appSettingKey, out var resolvedKey))
                    continue;
                requestObj.WritePropertyName(resolvedKey);
                WriteResolvedValue(requestObj, value, appSettingValue);
            }
            requestObj.WriteEndObject();
        }

        /// <summary>
        /// Decide whether the decorated parameter should serialize as an array of objects and,
        /// if so, the inner field names to use. Returns false (flat-object mode) for strings,
        /// dictionaries, and non-collection types.
        /// </summary>
        private bool TryGetArrayItemKeys(ParameterInfo parameter, out string nameKey, out string valueKey)
        {
            nameKey = ItemNameKey;
            valueKey = ItemValueKey;

            var type = parameter.ParameterType;
            if (type == typeof(string))
                return false;
            if (typeof(System.Collections.IDictionary).IsAssignableFrom(type))
                return false;

            var elementType = GetEnumerableElementType(type);
            if (elementType == null)
                return false;

            // Name the inner object fields from the element type's first two [JsonProperty]
            // members in declaration order; fall back to ItemNameKey/ItemValueKey.
            var jsonNames = elementType
                .GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .Where(member => member is FieldInfo || member is PropertyInfo)
                .Select(member => member.GetCustomAttribute<JsonPropertyAttribute>())
                .Where(attr => attr != null && attr.PropertyName.HasBlackSpace())
                .Select(attr => attr.PropertyName)
                .ToArray();
            if (jsonNames.Length >= 2)
            {
                nameKey = jsonNames[0];
                valueKey = jsonNames[1];
            }
            return true;
        }

        private static Type GetEnumerableElementType(Type type)
        {
            if (type.IsArray)
                return type.GetElementType();

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return type.GetGenericArguments()[0];

            var enumerableInterface = type.GetInterfaces()
                .FirstOrDefault(iface => iface.IsGenericType
                    && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            return enumerableInterface?.GetGenericArguments()[0];
        }

        private static bool TryResolveKey(string key, string appSettingKey, out string resolvedKey)
        {
            if (key.HasBlackSpace())
            {
                resolvedKey = key;
                return true;
            }
            if (appSettingKey.HasBlackSpace())
            {
                string extracted = null;
                var didExtract = appSettingKey.ConfigurationString(
                    value => { extracted = value; return true; },
                    why => false);
                resolvedKey = extracted;
                return didExtract;
            }
            resolvedKey = null;
            return false;
        }

        private static void WriteResolvedValue(JsonWriter requestObj, string value, string appSettingValue)
        {
            if (appSettingValue.HasBlackSpace())
            {
                _ = appSettingValue.ConfigurationString(
                    appSettingExtractedValue =>
                    {
                        requestObj.WriteValue(appSettingExtractedValue);
                        return true;
                    },
                    why =>
                    {
                        requestObj.WriteNull();
                        return false;
                    });
                return;
            }
            requestObj.WriteValue(value);
        }
    }
}

