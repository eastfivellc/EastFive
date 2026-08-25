using System;
using System.Reflection;

namespace EastFive.Serialization.Text
{
    public interface IMapTextProperty
    {
        bool DoesMap(string scope);
        TResource ParseRow<TResource>(TResource resource,
            MemberInfo member, (string key, string value)[] rowValues);
    }
}

