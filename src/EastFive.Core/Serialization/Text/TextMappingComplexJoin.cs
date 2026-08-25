using System;
using System.Data;
using System.Linq;
using System.Collections.Generic;

using Microsoft.VisualBasic.FileIO;

using EastFive.Linq;
using EastFive.Extensions;
using EastFive.Collections.Generic;
using System.Reflection.PortableExecutable;

namespace EastFive.Serialization.Text
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
    public class TextMappingComplexJoinAttribute : System.Attribute, IMapComplexTextJoin
    {
        public string MappingScope { get; set; }

        public double MatchingOrder { get; set; }

        public double ProcessingOrder { get; set; }

        public string MatchingColumn { get; set; }

        private string identifierColumn = default;
        /// <summary>
        /// The name of the header that, if present, denotes a match between
        /// the sheet and this join.
        /// </summary>
        public string IdentifierColumn
        {
            get
            {
                if (identifierColumn.HasBlackSpace())
                    return identifierColumn;
                return this.MatchingColumnThis;
            }
            set
            {
                this.identifierColumn = value;
            }
        }

        private string matchingColumnThis = default;
        /// <summary>
        /// The name of the column in lookup sheet (sheet matching this attribute)
        /// that should be used to check for a row match.
        /// </summary>
        public string MatchingColumnThis
        {
            get
            {
                if (matchingColumnThis.HasBlackSpace())
                    return matchingColumnThis;
                return this.MatchingColumn;
            }
            set
            {
                this.matchingColumnThis = value;
            }
        }

        private string matchingColumnMaster = default;
        /// <summary>
        /// The name of the column in master row from the primary sheet
        /// that should be used to check for a row match.
        /// </summary>
        public string MatchingColumnMaster
        {
            get
            {
                if (matchingColumnMaster.HasBlackSpace())
                    return matchingColumnMaster;
                return this.MatchingColumn;
            }
            set
            {
                this.matchingColumnMaster = value;
            }
        }

        public StringComparison ComparisonType { get; set; }

        public TextMappingComplexJoinAttribute(string mappingScope)
        {
            this.MappingScope = mappingScope;
        }

        public bool TryIndexLines<TResource>(TextFieldParser parser,
            string[] headers, IFilterText[] textFilters,
            out IDictionary<string, (string, string)[]> values)
        {
            if (!headers.Contains(this.IdentifierColumn, this.ComparisonType))
            {
                values = default;
                return false;
            }

            CrossCheck<TResource>(headers);

            values = IndexLinesInner()
                .ToDictionary((dict, dups) => dict);
            return true;

            IEnumerable<KeyValuePair<string, (string, string)[]>> IndexLinesInner()
            {
                while (!parser.EndOfData)
                {
                    KeyValuePair<string, (string, string)[]> resource;
                    try
                    {
                        var fields = parser.ReadFields();
                        var values = headers.CollateSimple(fields).ToArray();

                        var index = values
                            .Where(tpl => String.Equals(tpl.Item1, this.MatchingColumnThis, this.ComparisonType))
                            .First(
                                (tpl, next) => tpl.Item2,
                                () => default(string));
                        if (index.IsNullOrWhiteSpace())
                            continue;

                        var filter = textFilters
                            .Where(textFilter => !textFilter.Where(values))
                            .Any();
                        if (filter)
                            continue;

                        resource = index.PairWithValue(values);
                    }
                    catch (Exception ex)
                    {
                        ex.GetType();
                        continue;
                    }
                    yield return resource;
                }
            }
        }

        protected virtual void CrossCheck<TResource>(string[] headers)
        {
            if (!headers.Contains(this.MatchingColumnThis, this.ComparisonType))
                throw new Exception($"{this.GetType().FullName} has {nameof(IdentifierColumn)}={this.IdentifierColumn} but {nameof(MatchingColumnThis)} is not in {headers.Join(',')}.");

        }

        public virtual bool TryInnerJoin<TResource>((string key, string value)[] masterRow,
            IDictionary<string, (string key, string value)[]> indexDict,
            out (string key, string value)[] slaveValues)
        {
            bool returnValue;
            (returnValue, slaveValues) = masterRow
                .Where(kvp => String.Equals(kvp.key, this.MatchingColumnMaster, this.ComparisonType))
                .First(
                    (kvp, next) =>
                    {
                        if (!indexDict.TryGetValue(kvp.value, out (string, string)[] slaveRow))
                            return (false, default((string, string)[]));

                        return (true, slaveRow);
                    },
                    () => (false, default((string, string)[])));

            return returnValue;
        }
    }

    //public class TextMappingComplexJoin2Attribute : TextMappingComplexJoinAttribute
    //{
    //    public TextMappingComplexJoin2Attribute(string mappingScope) : base(mappingScope) { }
    //}

    //public class TextMappingComplexJoin3Attribute : TextMappingComplexJoinAttribute
    //{
    //    public TextMappingComplexJoin3Attribute(string mappingScope) : base(mappingScope) { }
    //}

    //public class TextMappingComplexJoin4Attribute : TextMappingComplexJoinAttribute
    //{
    //    public TextMappingComplexJoin4Attribute(string mappingScope) : base(mappingScope) { }
    //}

    //public class TextMappingComplexJoin5Attribute : TextMappingComplexJoinAttribute
    //{
    //    public TextMappingComplexJoin5Attribute(string mappingScope) : base(mappingScope) { }
    //}

    //public class TextMappingComplexJoin6Attribute : TextMappingComplexJoinAttribute
    //{
    //    public TextMappingComplexJoin6Attribute(string mappingScope) : base(mappingScope) { }
    //}

    //public class TextMappingComplexJoin7Attribute : TextMappingComplexJoinAttribute
    //{
    //    public TextMappingComplexJoin7Attribute(string mappingScope) : base(mappingScope) { }
    //}

    //public class TextMappingComplexJoin8Attribute : TextMappingComplexJoinAttribute
    //{
    //    public TextMappingComplexJoin8Attribute(string mappingScope) : base(mappingScope) { }
    //}

    //public class TextMappingComplexJoin9Attribute : TextMappingComplexJoinAttribute
    //{
    //    public TextMappingComplexJoin9Attribute(string mappingScope) : base(mappingScope) { }
    //}

    //public class TextMappingComplexJoin10Attribute : TextMappingComplexJoinAttribute
    //{
    //    public TextMappingComplexJoin10Attribute(string mappingScope) : base(mappingScope) { }
    //}

    //public class TextMappingComplexJoin11Attribute : TextMappingComplexJoinAttribute
    //{
    //    public TextMappingComplexJoin11Attribute(string mappingScope) : base(mappingScope) { }
    //}
}

