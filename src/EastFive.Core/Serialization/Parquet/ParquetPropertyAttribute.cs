using System;
using System.Linq;
using System.Reflection;

using Parquet.Data;

using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Reflection;
using EastFive.Serialization.Text;

namespace EastFive.Serialization.Parquet
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class ParquetPropertyAttribute : ScopedMapParquetPropertyAttribute
    {
        public string Name { get; set; }

        public StringComparison ComparisonType { get; set; }

        public ParquetPropertyAttribute()
        {
        }

        public ParquetPropertyAttribute(string name)
        {
            this.Name = name;
        }

        public override TResource ParseMemberValueFromRow<TResource>(TResource resource,
            MemberInfo member, (global::Parquet.Data.Field key, object value)[] rowValues)
        {
            var type = member.GetPropertyOrFieldType();
            var nameToMatch = this.Name.HasBlackSpace() ?
                this.Name
                :
                member.Name;
            var assignment = rowValues
                .Where(tpl => String.Equals(nameToMatch, tpl.key.Name, ComparisonType))
                .First(
                    (rowKeyValue, next) =>
                    {
                        var (rowKey, rowValue) = rowKeyValue;
                        var assign = member.ParseObjectAsAssignment<TResource>(type, rowValue, this.ComparisonType);
                        return assign;
                    },
                    () =>
                    {
                        Func<TResource, TResource> assign = (res) => res;
                        return assign;
                    });
            var updatedResource = assignment(resource);
            return updatedResource;
        }

        
    }

    public class ParquetPropertyDebugAttribute : ParquetPropertyAttribute
    {
        public override bool DoesMap(string scope)
        {
            return base.DoesMap(scope);
        }

        public override bool Match(object obj)
        {
            return base.Match(obj);
        }

        public override TResource ParseMemberValueFromRow<TResource>(TResource resource, MemberInfo member,
            (Field key, object value)[] rowValues)
        {
            return base.ParseMemberValueFromRow(resource, member, rowValues);
        }
    }
}

