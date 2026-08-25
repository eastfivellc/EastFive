using System;
using System.Linq;
using System.Reflection;

using EastFive.Linq;
using EastFive.Reflection;

namespace EastFive.Serialization.Parquet
{
    public interface IFilterParquet
    {
        bool DoesFilter(string scope);
        bool Where((global::Parquet.Data.Field key, object value)[] rowValues);
    }
}

