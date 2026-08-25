using System;
using System.Data;
using System.Threading.Tasks;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.IO;

using EastFive.Linq;
using EastFive.Reflection;
using EastFive.Linq.Async;

namespace EastFive.Serialization.Parquet
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
    public class ParquetMappingAttribute : ScopedMapParquetAttribute
    {
        public virtual (MemberInfo, IMapParquetProperty)[] GetPropertyMappers<TResource>()
        {
            return typeof(TResource)
                .GetPropertyOrFieldMembers()
                .Select(
                    member =>
                    {
                        var matchingAttrs = member
                            .GetAttributesInterface<IMapParquetProperty>()
                            .Where(attr => attr.DoesMap(this.Scope))
                            .ToArray();
                        return (member, matchingAttrs);
                    })
                .Where(tpl => tpl.matchingAttrs.Any())
                .Select(tpl => (tpl.member, attr: tpl.matchingAttrs.First()))
                .ToArray();
        }

        public override TResource[] Parse<TResource>(Stream parquetData,
            IFilterParquet[] textFilters,
            params Stream[] parquetDataJoins)
        {
            var membersAndMappers = GetPropertyMappers<TResource>();

            var table = global::Parquet.ParquetReader.ReadTableFromStream(parquetData);
            var headers = table.Schema.Fields;

            return Iterate().ToArray();

            IEnumerable<TResource> Iterate()
            {
                foreach (var row in table)
                {
                    TResource resource;
                    try
                    {
                        var fields = row.Values;
                        var properties = headers.CollateSimple(fields).ToArray();

                        resource = ParseResource(membersAndMappers, properties);

                        TResource ParseResource(
                            (MemberInfo, IMapParquetProperty)[] membersAndMappers,
                            (global::Parquet.Data.Field key, object value)[] rowValues)
                        {
                            var resource = Activator.CreateInstance<TResource>();
                            return membersAndMappers
                                .Aggregate(resource,
                                    (resource, memberAndMapper) =>
                                    {
                                        var (member, mapper) = memberAndMapper;
                                        return mapper.ParseMemberValueFromRow(resource, member, rowValues);
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
    }
}

