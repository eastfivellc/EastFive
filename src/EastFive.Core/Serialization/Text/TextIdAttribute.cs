using System;
using System.Linq;
using System.Reflection;

using EastFive.Linq;
using EastFive.Reflection;

namespace EastFive.Serialization.Text
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class TextIdAttribute : ScopedMapTextPropertyAttribute
    {
        public string Fields { get; set; }

        public string Join { get; set; } = "_";

        public string BlankValue { get; set; } = "__";

        public StringComparison ComparisonType { get; set; }

        public override TResource ParseRow<TResource>(TResource resource, MemberInfo member, (string key, string value)[] rowValues)
        {
            var guidValue = Fields
                .Split(',')
                .Select(
                    field =>
                    {
                        return rowValues
                            .Where(tpl => String.Equals(field, tpl.key, ComparisonType))
                            .First(
                                (rowKeyValue, next) =>
                                {
                                    var (rowKey, rowValue) = rowKeyValue;
                                    return rowValue;
                                },
                                () => this.BlankValue);
                    })
                .Join(this.Join)
                .MD5HashGuid();

            var type = member.GetPropertyOrFieldType();
            if (!guidValue.TryCastRef(type, out object newValue))
                throw new Exception($"{nameof(TextIdAttribute)} cannot parse {type.FullName} on {member.DeclaringType.FullName}..{member.Name}");

            member.SetValue(ref resource, newValue);
            return resource;
        }
    }
}

