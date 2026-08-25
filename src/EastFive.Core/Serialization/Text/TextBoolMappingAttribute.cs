using System;
using System.Linq;
using System.Reflection;
using EastFive.Linq;
using EastFive.Reflection;

namespace EastFive.Serialization.Text
{
    public class TextBoolMappingAttribute : ScopedMapTextPropertyAttribute
    {
        public string Field { get; set; }

        public string Key1 { get; set; }
        public bool Value1 { get; set; }
        public string Key2 { get; set; }
        public bool Value2 { get; set; }
        public string Key3 { get; set; }
        public bool Value3 { get; set; }
        public string Key4 { get; set; }
        public bool Value4 { get; set; }
        public string Key5 { get; set; }
        public bool Value5 { get; set; }
        public string Key6 { get; set; }
        public bool Value6 { get; set; }
        public string Key7 { get; set; }
        public bool Value7 { get; set; }
        public bool BlankValue { get; set; } = false;

        public StringComparison ComparisonType { get; set; }

        public override TResource ParseRow<TResource>(TResource resource, MemberInfo member, (string key, string value)[] rowValues)
        {
            var propsAndFields = typeof(TextBoolMappingAttribute)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .ToArray();

            var assignment = rowValues
                .Where(tpl => String.Equals(this.Field, tpl.key, ComparisonType))
                .First(
                    (rowKeyValue, next) =>
                    {
                        var (rowKey, rowValue) = rowKeyValue;
                        var newValue = propsAndFields
                            .Where(prop => prop.Name.StartsWith("Key"))
                            .Where(
                                keyProp =>
                                {
                                    var keyValue = (string)keyProp.GetValue(this);
                                    return string.Equals(rowValue, keyValue, this.ComparisonType);
                                })
                            .First(
                                (keyProp, next) =>
                                {
                                    var valueProp = propsAndFields
                                        .Where(prop => prop.Name.StartsWith("Value"))
                                        .Where(valueProp => valueProp.Name.Last() == keyProp.Name.Last())
                                        .First();
                                    return (bool)valueProp.GetValue(this);
                                },
                                () =>
                                {
                                    return BlankValue;
                                });


                        Func<TResource, TResource> assign = (res) =>
                            (TResource)member.SetPropertyOrFieldValue(res, newValue);
                        return assign;
                    },
                    () =>
                    {
                        Func<TResource, TResource> assign = (res) => res;
                        return assign;
                    });

            return assignment(resource);
        }
    }
}

