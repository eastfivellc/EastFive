using System;
using System.Data;
using System.Linq;
using System.Collections.Generic;

using Microsoft.VisualBasic.FileIO;

using EastFive.Linq;
using EastFive.Extensions;
using EastFive.Collections.Generic;

namespace EastFive.Serialization.Text
{
    public class TextMappingComplexMultipleValuesJoinAttribute : TextMappingComplexJoinAttribute
    {
        public string MatchingColumns { get; set; }

        private string matchingColumnsMaster = default;
        public string MatchingColumnsMaster
        {
            get
            {
                if (matchingColumnsMaster.HasBlackSpace())
                    return matchingColumnsMaster;
                return this.MatchingColumns;
            }
            set
            {
                this.matchingColumnsMaster = value;
            }
        }

        public TextMappingComplexMultipleValuesJoinAttribute(string mappingScope) : base(mappingScope) { }

        public override bool TryInnerJoin<TResource>((string key, string value)[] masterRow,
            IDictionary<string, (string key, string value)[]> indexDict,
            out (string key, string value)[] slaveValues)
        {
            bool returnValue;
            var matchingValues = this.MatchingColumnsMaster.Split(',').Select(s => s.Trim()).ToArray();
            (returnValue, slaveValues) = masterRow
                .Where(kvp => matchingValues.Contains(kvp.key, this.ComparisonType))
                .First(
                    (kvp, next) =>
                    {
                        if (!indexDict.TryGetValue(kvp.value, out (string, string)[] slaveRow))
                            return next();

                        return (true, slaveRow);
                    },
                    () => (false, default((string, string)[])));

            return returnValue;
        }

    }
}

