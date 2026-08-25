using System;
using System.Collections.Generic;
using System.IO;
using EastFive.Linq.Async;

namespace EastFive.Serialization.Parquet
{
    public interface IMapParquet
    {
        bool DoesParse(string scope);

        TResource[] Parse<TResource>(Stream parquetData,
            IFilterParquet[] textFilters,
            params Stream[] parquetDataJoins);
    }
}
