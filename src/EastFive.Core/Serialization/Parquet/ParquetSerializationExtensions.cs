using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Linq.Async;
using Parquet;
using Parquet.Data;

namespace EastFive.Serialization.Parquet
{
	public static class ParquetSerializationExtensions
	{
		public static TResource[] ParseParquet<TResource>(this Stream parquetData,
            string scope, Stream[] extraStreams )
		{
            var filters = typeof(TResource)
                .GetAttributesInterface<IFilterParquet>()
                .Where(attrInter => attrInter.DoesFilter(scope))
                .ToArray();

            return typeof(TResource)
                .GetAttributesInterface<IMapParquet>()
                .Where(attrInter => attrInter.DoesParse(scope))
                .First<IMapParquet, TResource[]>(
                    (attrInter, next) =>
                    {
                        var resources = attrInter.Parse<TResource>(parquetData, filters, extraStreams);
                        return resources;
                    },
                    () =>
                    {
                        var msg = $"{typeof(TResource).FullName} does not have an attribute implementing {nameof(IMapParquet)}.";
                        throw new Exception(msg);
                    });
        }

        public static void WriteToParquetStream(this IEnumerable<(string name, Type type, object value)[]> rows, Schema schema, Stream stream)
        {
            using (var writer = new ParquetWriter(schema, stream))
            {
                var table = rows
                    .Aggregate(
                        new global::Parquet.Data.Rows.Table(schema),
                        (table, row) =>
                        {
                            var values = row
                                .Select(
                                    col =>
                                    {
                                        if (col.value.IsNull())
                                            return col.value;

                                        if (col.value.GetType() == typeof(System.DBNull))
                                            return null;

                                        if (col.value.GetType() == typeof(DateTime))
                                            return new DateTimeOffset(((DateTime)col.value));

                                        //if (col.type == typeof(DateTime))
                                        //    if (col.value.GetType() == typeof(DateTimeOffset))
                                        //        return ((DateTimeOffset)col.value).DateTime;

                                        return col.value;
                                    });
                            var parquetRow = new global::Parquet.Data.Rows.Row(values);
                            table.Add(parquetRow);
                            return table;
                        });
                writer.Write(table);
            }
        }

    }
}

