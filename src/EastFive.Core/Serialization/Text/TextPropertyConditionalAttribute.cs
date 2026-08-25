using System;
using System.Linq;
using System.Reflection;

using EastFive.Linq;
using EastFive.Reflection;
using Newtonsoft.Json.Linq;

namespace EastFive.Serialization.Text
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class TextPropertyConditionalAttribute : ScopedMapTextPropertyAttribute
    {
        public string Condition1 { get; set; }
        public string Values1 { get; set; }
        public string Concatination1 { get; set; }

        public string Condition2 { get; set; }
        public string Values2 { get; set; }
        public string Concatination2 { get; set; }

        public string Condition3 { get; set; }
        public string Values3 { get; set; }
        public string Concatination3 { get; set; }

        public string ValuesDefault { get; set; }
        public string ConcatinationDefault { get; set; }

        public StringComparison ComparisonType { get; set; }

        public override TResource ParseRow<TResource>(TResource resource,
            MemberInfo member, (string key, string value)[] rowValues)
        {
            var type = member.GetPropertyOrFieldType();
            var attr = this;
            var assignment = GetAssignment();
            return assignment(resource);

            Func<TResource, TResource> GetAssignment()
            {
                if (TryExtract(attr.Condition1, attr.Values1, attr.Concatination1,
                                out Func<TResource, TResource> assign1))
                    return assign1;
                if (TryExtract(attr.Condition2, attr.Values2, attr.Concatination2,
                        out Func<TResource, TResource> assign2))
                    return assign2;
                if (TryExtract(attr.Condition3, attr.Values3, attr.Concatination3,
                        out Func<TResource, TResource> assign3))
                    return assign3;
                if (ValuesDefault.HasBlackSpace())
                {
                    var assignDefault = GetAssignment(ValuesDefault, ConcatinationDefault);
                    return assignDefault;
                }

                Func<TResource, TResource> assignNoMatch = (res) => res;
                return assignNoMatch;

                bool TryExtract(string condition, string values, string concatination,
                    out Func<TResource, TResource> assign)
                {
                    if (condition.HasBlackSpace())
                    {
                        var conditionValue = GetValue(condition);
                        if (conditionValue.HasBlackSpace())
                        {
                            assign = GetAssignment(values, concatination);
                            return true;
                        }
                    }
                    assign = default;
                    return false;
                }

                Func<TResource, TResource> GetAssignment(string values, string concatination)
                {
                    var value = values
                                .Split(',')
                                .Select(GetValue)
                                .Join(concatination);
                    var assign = ParseAssignment<TResource>(type, member, value, this.ComparisonType);
                    return assign;
                }

                string GetValue(string key)
                {
                    return rowValues
                        .Where(tpl => String.Equals(key, tpl.key, ComparisonType))
                        .First(
                            (rowKeyValue, next) =>
                            {
                                var (rowKey, rowValue) = rowKeyValue;
                                return rowValue;
                            },
                            () =>
                            {
                                return string.Empty;
                            });
                }
            }
        }

        public static Func<TResource, TResource> ParseAssignment<TResource>(Type type, MemberInfo member, string rowValue,
            StringComparison comparisonType)
        {
            if (typeof(string).IsAssignableTo(type))
            {
                Func<TResource, TResource> assign = (res) =>
                    (TResource)member.SetPropertyOrFieldValue(res, rowValue);
                return assign;
            }
            if (typeof(Guid).IsAssignableTo(type))
            {
                Guid.TryParse(rowValue, out Guid guidValue);
                Func<TResource, TResource> assign = (res) =>
                    (TResource)member.SetPropertyOrFieldValue(res, guidValue);
                return assign;
            }
            if (typeof(DateTime).IsAssignableTo(type))
            {
                DateTime.TryParse(rowValue, out DateTime dtValue);
                Func<TResource, TResource> assign = (res) =>
                    (TResource)member.SetPropertyOrFieldValue(res, dtValue);
                return assign;
            }
            if (typeof(bool).IsAssignableTo(type))
            {
                bool.TryParse(rowValue, out bool boolValue);
                Func<TResource, TResource> assign = (res) =>
                    (TResource)member.SetPropertyOrFieldValue(res, boolValue);
                return assign;
            }
            if (type.IsEnum)
            {
                var ignoreCase = comparisonType == StringComparison.OrdinalIgnoreCase
                    || comparisonType == StringComparison.InvariantCultureIgnoreCase
                    || comparisonType == StringComparison.CurrentCultureIgnoreCase;
                if (Enum.TryParse(type, rowValue, ignoreCase, out object newValue))
                {
                    Func<TResource, TResource> assign = (res) =>
                        (TResource)member.SetPropertyOrFieldValue(res, newValue);
                    return assign;
                }
                Func<TResource, TResource> noAssign = (res) => res;
                return noAssign;
            }
            if (rowValue.TryParseRef(type, out object refObj, out var didMatch))
            {
                Func<TResource, TResource> assign = (res) =>
                    (TResource)member.SetPropertyOrFieldValue(res, rowValue);
                return assign;
            }
            if (didMatch)
            {
                Func<TResource, TResource> assign = (res) => res;
                return assign;
            }
            throw new Exception($"{nameof(TextPropertyAttribute)} cannot parse {type.FullName} on {member.DeclaringType.FullName}..{member.Name}");
        }
    }

    //public class TextPropertyConditional2Attribute : TextPropertyConditionalAttribute { };
    //public class TextPropertyConditional3Attribute : TextPropertyConditionalAttribute { };
    //public class TextPropertyConditional4Attribute : TextPropertyConditionalAttribute { };
    //public class TextPropertyConditional5Attribute : TextPropertyConditionalAttribute { };
    //public class TextPropertyConditional6Attribute : TextPropertyConditionalAttribute { };
    //public class TextPropertyConditional7Attribute : TextPropertyConditionalAttribute { };
    //public class TextPropertyConditional8Attribute : TextPropertyConditionalAttribute { };
}

