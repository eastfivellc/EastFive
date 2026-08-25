using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using EastFive.Linq;

namespace EastFive.Serialization.DataReader
{
	public static class DataReaderExtensions
    {
		public static IEnumerable<TResource> Bind<TResource>(this IDataReader dataReader)
        {
            return typeof(TResource)
                .GetAttributesInterface<IMapDataReader>()
                .First<IMapDataReader, IEnumerable<TResource>>(
                    (attrInter, next) =>
                    {
                        var resources = attrInter.Parse<TResource>(dataReader);
                        return resources;
                    },
                    () =>
                    {
                        var msg = $"{typeof(TResource).FullName} does not have an attribute implementing {nameof(IMapDataReader)}.";
                        throw new Exception(msg);
                    });
        }

	}
}

