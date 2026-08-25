using System;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.IO;

using Microsoft.VisualBasic.FileIO;

using EastFive;
using EastFive.Linq;
using EastFive.Reflection;
using EastFive.Extensions;
using EastFive.Collections.Generic;

namespace EastFive.Serialization.Text
{
    public interface IMapComplexTextJoin
    {
        string MappingScope { get; set; }

        double MatchingOrder { get; }

        double ProcessingOrder { get; }

        bool TryIndexLines<TResource>(TextFieldParser parser,
            string[] headers, IFilterText[] textFilters,
            out IDictionary<string, (string, string)[]> values);

        bool TryInnerJoin<TResource>((string key, string value)[] masterRow,
            IDictionary<string, (string key, string value)[]> indexDict,
            out (string key, string value)[] slaveValues);
    }
}

