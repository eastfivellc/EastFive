using System;
using System.Linq;
using System.Reflection;

using EastFive.Linq;
using EastFive.Reflection;

namespace EastFive.Serialization.Text
{
    public interface IFilterText
    {
        bool DoesFilter(string scope);
        bool Where((string key, string value)[] rowValues);
    }
}

