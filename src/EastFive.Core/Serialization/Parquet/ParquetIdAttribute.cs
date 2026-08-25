using System;
using System.Linq;
using System.Reflection;

using EastFive.Linq;
using EastFive.Reflection;

namespace EastFive.Serialization.Parquet
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class ParquetIdAttribute : ScopedMapParquetPropertyAttribute
    {
        public string Fields { get; set; }

        public string Join { get; set; } = "_";

        public string BlankValue { get; set; } = "--";

        public string Prefix { get; set; } = "";

        public string Postfix { get; set; } = "";

        public StringComparison ComparisonType { get; set; }

        public override TResource ParseMemberValueFromRow<TResource>(TResource resource, MemberInfo member, (global::Parquet.Data.Field key, object value)[] rowValues)
        {
            var guidValue = Fields
                .Split(',')
                .Select(
                    field =>
                    {
                        return rowValues
                            .Where(tpl => String.Equals(field, tpl.key.Name, ComparisonType))
                            .First(
                                (rowKeyValue, next) =>
                                {
                                    var (rowKey, rowValue) = rowKeyValue;
                                    var rowValueStr = rowValue.ToString();
                                    return rowValueStr;
                                },
                                () => this.BlankValue);
                    })
                .Prepend(this.Prefix)
                .Append(this.Postfix)
                .Join(this.Join)
                .MD5HashGuid();

            var type = member.GetPropertyOrFieldType();
            if (!guidValue.TryCastRef(type, out object newValue))
                throw new Exception($"{nameof(ParquetIdAttribute)} cannot parse {type.FullName} on {member.DeclaringType.FullName}..{member.Name}");

            member.SetValue(ref resource, newValue);
            return resource;
        }
    }
}

