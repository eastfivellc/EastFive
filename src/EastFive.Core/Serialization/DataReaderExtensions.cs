using System;
using System.Data;
using System.Diagnostics;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;

using EastFive.Serialization.DataReader;
using Newtonsoft.Json;
using System.Dynamic;

namespace EastFive.Serialization
{
	public static class DataReaderExtensions
	{
        public delegate bool TryParseTextValueDelegate(Type type, object value, out string parsedValue);

        public static IEnumerable<(string name, Type type, object value)[]> ReadAsTuple(this IDataReader dataReader)
        {
            var fieldCount = dataReader.FieldCount;

            while (dataReader.Read())
            {
                yield return Enumerable
                    .Range(0, fieldCount)
                    .Select(
                        index =>
                        {
                            var name = dataReader.GetName(index);
                            var value = dataReader.GetValue(index);
                            var type = dataReader.GetFieldType(index);
                            return (name, type, value);
                        })
                    .ToArray();
            }
        }

        public static IEnumerable<string[]> ReadAsTextValues(this IDataReader dataReader,
            TryParseTextValueDelegate tryParseValue)
		{
            if (!dataReader.Read())
                yield break;

            var fieldCount = dataReader.FieldCount;
            var headerRow = Enumerable
                .Range(0, fieldCount)
                .Select(
                    index =>
                    {
                        var column = dataReader.GetName(index);
                        return column;
                    })
                .ToArray();

            yield return headerRow;

            while (dataReader.Read())
            {
                string[] populatedRow = default;
                try
                {
                    populatedRow = Enumerable
                        .Range(0, fieldCount)
                        .Select(
                            index =>
                            {
                                var value = dataReader.GetValue(index);
                                var type = dataReader.GetFieldType(index);
                                if (tryParseValue(type, value, out var parsedValue))
                                    return parsedValue;

                                return value.ToString();
                            })
                        .ToArray();
                }
                catch (Exception ex)
                {
                    ex.GetType();
                    continue;
                }
                yield return populatedRow;
            }
        }

        public static IEnumerable<string> ReadAsJsonRows(this IDataReader dataReader)
        {
            var fieldCount = dataReader.FieldCount;

            while (dataReader.Read())
            {
                dynamic myobject = new ExpandoObject();
                try
                {
                    var myUnderlyingObject = Enumerable
                        .Range(0, fieldCount)
                        .Aggregate((IDictionary<string, object>)myobject,
                            (myUnderlyingObject, index) =>
                            {
                                var name = dataReader.GetName(index);
                                var value = dataReader.GetValue(index);
                                var type = dataReader.GetFieldType(index);
                                myUnderlyingObject.Add(name, value); // Adding dynamically named property

                                return myUnderlyingObject;
                            })
                        .ToArray();
                }
                catch (Exception ex)
                {
                    ex.GetType();
                    continue;
                }

                var jsonString = JsonConvert.SerializeObject(myobject);
                yield return jsonString;
            }
        }

        public static IEnumerable<string> ReadAsSqlStatements(this IDataReader dataReader, string tableName, string key)
        {
            var fieldCount = dataReader.FieldCount;

            while (dataReader.Read())
            {
                var statement = "";
                try
                {
                    var myUnderlyingObject = Enumerable
                        .Range(0, fieldCount)
                        .Select(
                            (index) =>
                            {
                                var name = dataReader.GetName(index);
                                var value = dataReader.GetValue(index);
                                var type = dataReader.GetFieldType(index);
                                return (name, value);
                            })
                        .ToArray();

                    var names = myUnderlyingObject.Select(tpl => tpl.name.ToString().Replace("'", "''")).Join(',');
                    var values = myUnderlyingObject.Select(tpl => tpl.value.ToString().Replace("'", "''")).Join(',');
                    var updates = myUnderlyingObject.Select(tpl => $"{tpl.name} = {tpl.value.ToString().Replace("'", "''")}").Join(',');
                    statement = $"MERGE {tableName} AS target" +
                        $" USING (SELECT {values}) AS source ({names}) ON (target.{key}= source.{key})" +
                        $" WHEN MATCHED THEN UPDATE SET {updates}" +
                        $" WHEN NOT MATCHED THEN INSERT ({names}) VALUES ({values})";
                }
                catch (Exception ex)
                {
                    ex.GetType();
                    continue;
                }
                yield return statement;
            }
        }

        public static global::Parquet.Data.Schema GetParquetSchema(this IDataReader dataReader)
        {
            var fieldCount = dataReader.FieldCount;
            var fields = Enumerable
                .Range(0, fieldCount)
                .Select(
                    (index) =>
                    {
                        var name = dataReader.GetName(index);
                        var type = dataReader.GetFieldType(index);
                        if (type == typeof(DateTime))
                            type = typeof(DateTime?);
                        if (type == typeof(long))
                            type = typeof(long?);
                        if (type == typeof(int))
                            type = typeof(int?);
                        if (type == typeof(bool))
                            type = typeof(bool?);
                        if (type == typeof(float))
                            type = typeof(float?);
                        if (type == typeof(double))
                            type = typeof(double?);
                        if (type == typeof(decimal))
                            type = typeof(decimal?);
                        return new global::Parquet.Data.DataField(name, type);
                    })
                .ToArray();

            return new global::Parquet.Data.Schema(fields);
        }
    }
}