using System;
using System.Collections.Generic;
using System.IO;

namespace EastFive.Serialization.Text
{
    public interface IMapText
    {
        bool DoesParse(string scope);

        IEnumerable<TResource> Parse<TResource>(Stream csvData,
            IFilterText[] textFilters, 
            params Stream[] csvDataJoins);
    }
}
