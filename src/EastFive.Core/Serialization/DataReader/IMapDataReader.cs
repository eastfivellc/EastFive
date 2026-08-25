using System;
using System.Collections.Generic;
using System.Data;

namespace EastFive.Serialization.DataReader
{
    public interface IMapDataReader
    {
        IEnumerable<TResource> Parse<TResource>(IDataReader dataReader);
    }
}

