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

using EastFive;
using EastFive.Linq;
using EastFive.Reflection;
using EastFive.Extensions;
using EastFive.Collections.Generic;

namespace EastFive.Serialization.Text
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
    public class TextMappingComplexJoinsAttribute : TextMappingAttribute
    {
        public string MappingScope { get; set; }

        public bool DiscardUnmatched { get; set; } = false;

        public TextMappingComplexJoinsAttribute(string mappingScope)
        {
            this.MappingScope = mappingScope;
        }

        public override IEnumerable<TResource> Parse<TResource>(Stream csvDataSource,
            IFilterText[] textFilters,
            params Stream[] csvDataJoins)
        {
            var membersAndMappers = GetPropertyMappers<TResource>();
            var joiners = typeof(TResource)
                .GetAttributesInterface<IMapComplexTextJoin>()
                .Where(joiner => String.Equals(joiner.MappingScope, this.MappingScope, StringComparison.Ordinal))
                .OrderBy(joiner => joiner.MatchingOrder);

            var joinLookups = csvDataJoins
                .Select(ParseLookup)
                .OrderBy(tpl => tpl.Item1.ProcessingOrder)
                .ToArray();

            using (var parser = new TextFieldParser(csvDataSource))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");
                var headers = parser
                    .ReadLine()
                    .Split(',');

                return ParseResources();

                IEnumerable<TResource> ParseResources()
                {
                    while (!parser.EndOfData)
                    {
                        TResource resource;
                        try
                        {
                            var fields = parser.ReadFields();
                            var rowValues = headers.CollateSimple(fields).ToArray();

                            var (allMatched, joinedValues) = joinLookups
                                .Aggregate(
                                    (true, rowValues),
                                    (allMatchedRowValuesTpl, joinLookupTpl) =>
                                    {
                                        var (allMatched, rowValues) = allMatchedRowValuesTpl;
                                        var (join, lookup) = joinLookupTpl;

                                        var didGet = join.TryInnerJoin<TResource>(rowValues, lookup,
                                            out (string, string)[] slaveValues);

                                        if (!didGet)
                                            return (false, rowValues);

                                        var updatedRowValues = rowValues.Concat(slaveValues).ToArray();
                                        return (allMatched, updatedRowValues);
                                    });

                            if (this.DiscardUnmatched && (!allMatched))
                                continue;

                            resource = ParseResource(membersAndMappers, joinedValues);
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

            (IMapComplexTextJoin, IDictionary<string, (string, string)[]>) ParseLookup(Stream csvData)
            {
                using (var parser = new TextFieldParser(csvData))
                {
                    parser.TextFieldType = FieldType.Delimited;
                    parser.SetDelimiters(",");
                    var headers = parser
                        .ReadLine()
                        .Split(',');

                    return joiners
                        .ToArray()
                        .First<IMapComplexTextJoin,(IMapComplexTextJoin, IDictionary<string, (string, string)[]>)>(
                            (joiner, next) =>
                            {
                                if (!joiner.TryIndexLines<TResource>(parser,
                                    headers, textFilters, out IDictionary<string, (string, string)[]> values))
                                    return next();
                                return (joiner, values);
                            },
                            () =>
                            {
                                throw new Exception($"No matching joiner to parse file with headers:{headers.Join(',')}");
                            });
                }
            }
        }
    }
}

