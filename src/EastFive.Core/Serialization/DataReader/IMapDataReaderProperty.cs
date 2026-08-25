using System;
using System.Data;
using System.Reflection;

namespace EastFive.Serialization.DataReader
{
    public interface IMapDataReaderProperty
    {
        TResource ParseMemberValueFromRow<TResource>(TResource resource,
            MemberInfo member, DataTable schema, IDataReader dataReader);
    }
}

