using System;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Reflection.PortableExecutable;
using EastFive.Reflection;

namespace EastFive.Serialization.DataReader
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class DataReaderPropertyAttribute : System.Attribute, IMapDataReaderProperty
    {
        public string Name { get; set; }

        public StringComparison ComparisonType { get; set; } = StringComparison.OrdinalIgnoreCase;

        public DataReaderPropertyAttribute()
        {
        }

        public DataReaderPropertyAttribute(string name)
        {
            this.Name = name;
        }

        public TResource ParseMemberValueFromRow<TResource>(TResource resource,
            MemberInfo member, DataTable schema, IDataReader dataReader)
        {
            var memberType = member.GetPropertyOrFieldType();
            var nameToMatch = this.Name.HasBlackSpace() ?
                this.Name
            :
            member.Name;

            var populatedResource = Enumerable
                .Range(0, dataReader.FieldCount)
                .Select(
                    i =>
                    {
                        var column = dataReader.GetName(i);
                        return (index: i, column: column);
                    })
                .Where(
                    dataColumn =>
                    {
                        if (String.Equals(nameToMatch, dataColumn.column, ComparisonType))
                            return true;
                        return false;
                    })
                .Aggregate(resource,
                    (resource, columnTpl) =>
                    {
                        var (index, column) = columnTpl;
                        var valueType = dataReader.GetFieldType(index);
                        if(memberType.IsAssignableFrom(valueType))
                        {
                            var value = dataReader.GetValue(index);
                            if (value is System.DBNull)
                                value = memberType.GetDefault();
                            var updatedResource = (TResource)member.SetPropertyOrFieldValue(resource, value);
                            return updatedResource;
                        }
                        return resource;
                    });
            return populatedResource;
        }
    }
}

