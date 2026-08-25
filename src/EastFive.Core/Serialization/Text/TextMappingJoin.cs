using System;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.IO;

using Microsoft.VisualBasic.FileIO;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using EastFive.Linq;
using EastFive.Reflection;
using EastFive.Extensions;
using EastFive.Collections.Generic;

namespace EastFive.Serialization.Text
{
    public class TextMappingJoinAttribute : TextMappingAttribute
    {
        public string MatchingColumn { get; set; }

        public StringComparison ComparisonType { get; set; }

        public bool DiscardUnmatched { get; set; } = false;

        public TextMappingJoinAttribute(string matchingColumn)
        {
            this.MatchingColumn = matchingColumn;
        }

        public override IEnumerable<TResource> Parse<TResource>(Stream csvDataSource,
            IFilterText[] textFilters,
            params Stream[] csvDataJoins)
        {
            var joinLookups = csvDataJoins
                .Select(ParseLookup)
                .ToArray();

            var membersAndMappers = GetPropertyMappers<TResource>();

            using (var parser = new TextFieldParser(csvDataSource))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");
                var headers = parser
                    .ReadLine()
                    .Split(',');

                return IndexLines(parser, headers)
                    .Select(
                        kvp =>
                        {
                            var (index, values) = kvp;
                            if (!this.DiscardUnmatched)
                            {
                                var joinedValuesNoDiscard = joinLookups
                                    .TrySelect(
                                        (IDictionary<string, (string, string)[]> indexDict, out (string, string)[] values) =>
                                        {
                                            return indexDict.TryGetValue(index, out values);
                                        })
                                    .SelectMany()
                                    .Concat(values)
                                    .ToArray();
                                return (true, joinedValuesNoDiscard);
                            }

                            var joinValues = joinLookups
                                .SelectWith<bool, IDictionary<string, (string, string)[]>, (string, string)[]>(true,
                                    (bool value, IDictionary<string, (string, string)[]> indexDict, out bool updatedValue) =>
                                    {
                                        var didGet = indexDict.TryGetValue(index, out (string, string)[] matchingValues);
                                        updatedValue = value && didGet;
                                        return matchingValues;
                                    },
                                    out bool allMatched);
                            if (!allMatched)
                                return (false, new (string, string)[] { });

                            var joinedValues = joinValues
                                .SelectMany()
                                .Concat(values)
                                .ToArray();
                            return (true, joinedValues);
                        })
                    .SelectWhere()
                    .Select(
                        rowValues =>
                        {
                            return ParseResource(membersAndMappers, rowValues);
                        });

                TResource ParseResource(
                        (MemberInfo, IMapTextProperty)[] membersAndMappers,
                        (string key, string value)[] rowValues)
                {
                    var resource = Activator.CreateInstance<TResource>();
                    return membersAndMappers
                        .Aggregate(resource,
                            (resource, memberAndMapper) =>
                            {
                                var (member, mapper) = memberAndMapper;
                                return mapper.ParseRow(resource, member, rowValues);
                            });
                }
            }

            IDictionary<string, (string, string)[]> ParseLookup(Stream csvData)
            {
                using (var parser = new TextFieldParser(csvData))
                {
                    parser.TextFieldType = FieldType.Delimited;
                    parser.SetDelimiters(",");
                    var headers = parser
                        .ReadLine()
                        .Split(',');

                    return IndexLines(parser, headers)
                        .ToDictionary((dict, dups) => dict);
                }
            }

            IEnumerable<KeyValuePair<string, (string, string)[]>> IndexLines(
                TextFieldParser parser, string[] headers)
            {
                while (!parser.EndOfData)
                {
                    KeyValuePair<string, (string, string)[]> resource;
                    try
                    {
                        var fields = parser.ReadFields();
                        var values = headers.CollateSimple(fields).ToArray();

                        var index = values
                            .Where(tpl => String.Equals(tpl.Item1, this.MatchingColumn, this.ComparisonType))
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
    }
}

