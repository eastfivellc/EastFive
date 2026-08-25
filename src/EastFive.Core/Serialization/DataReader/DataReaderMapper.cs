using System;
using System.Collections.Generic;
using System.Data;
using EastFive.Linq;
using EastFive.Serialization.Parquet;
using System.Linq;
using System.Reflection;
using EastFive.Reflection;

namespace EastFive.Serialization.DataReader
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
    public class DataReaderMapper : System.Attribute, IMapDataReader
	{
		public DataReaderMapper()
		{
		}

        public IEnumerable<TResource> Parse<TResource>(IDataReader dataReader)
        {
            var membersAndMappers = GetPropertyMappers<TResource>();
            var schema = dataReader.GetSchemaTable();
            return Iterate();

            IEnumerable<TResource> Iterate()
            {
                while(dataReader.Read())
                {
                    TResource resource;
                    try
                    {
                        resource = ParseResource(membersAndMappers);

                        TResource ParseResource(
                            (MemberInfo, IMapDataReaderProperty)[] membersAndMappers)
                        {
                            var resource = Activator.CreateInstance<TResource>();
                            return membersAndMappers
                                .Aggregate(resource,
                                    (resource, memberAndMapper) =>
                                    {
                                        var (member, mapper) = memberAndMapper;
                                        return mapper.ParseMemberValueFromRow(resource, member, schema, dataReader);
                                    });
                        }
                    }
                    catch (Exception ex)
                    {
                        ex.GetType();
                        continue;
                    }
                    yield return resource;
                }
            }
        }

        public virtual (MemberInfo, IMapDataReaderProperty)[] GetPropertyMappers<TResource>()
        {
            return typeof(TResource)
                .GetPropertyOrFieldMembers()
                .Select(
                    member =>
                    {
                        var matchingAttrs = member
                            .GetAttributesInterface<IMapDataReaderProperty>()
                            .ToArray();
                        return (member, matchingAttrs);
                    })
                .Where(tpl => tpl.matchingAttrs.Any())
                .Select(tpl => (tpl.member, attr: tpl.matchingAttrs.First()))
                .ToArray();
        }
    }
}

