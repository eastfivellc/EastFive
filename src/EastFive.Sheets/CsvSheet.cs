using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using CsvHelper;

using EastFive;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Serialization;

namespace EastFive.Sheets
{
    public class CsvSheet : ISheet
    {
        private readonly Stream stream;

        public CsvSheet(Stream stream)
        {
            this.stream = stream;
        }

        public string Name => "sheet";

        public IEnumerable<string[]> ReadRows(
            Func<Type, object, Func<string>, string> discardSerializer = default,
            bool autoDecodeEncoding = default,
            Encoding[] encodingsToUse = default)
        {
            stream.Seek(0, SeekOrigin.Begin);
            var rawData = stream.ToBytes();
            var encoding = autoDecodeEncoding || encodingsToUse.NullToEmpty().Count() > 1
                ? DecodeEncoding(rawData, encodingsToUse)
                : encodingsToUse.AnyNullSafe()
                    ? encodingsToUse.First()
                    : default(Encoding);

            using (var rawDataStream = new MemoryStream(rawData))
            {
                using (var parser = encoding.IsNotDefaultOrNull()?
                    new Microsoft.VisualBasic.FileIO.TextFieldParser(rawDataStream, encoding)
                    :
                    new Microsoft.VisualBasic.FileIO.TextFieldParser(rawDataStream))
                {
                    parser.TextFieldType = Microsoft.VisualBasic.FileIO.FieldType.Delimited;
                    parser.SetDelimiters(",");
                    while (!parser.EndOfData)
                    {
                        string[] fields = new string[0];
                        try
                        {
                            fields = parser.ReadFields();
                        }
                        catch (Exception)
                        {
                            continue;
                        }
                        yield return fields;
                    }
                }
            }
        }

        public Encoding DecodeEncoding(byte[] rawData, Encoding[] encodings)
        {
            if (!encodings.AnyNullSafe())
                encodings = Encoding
                    .GetEncodings()
                    .Select(encodingInfo => Encoding.GetEncoding(encodingInfo.CodePage))
                    .ToArray();

            var profileCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";
            
            var encodingProfileNewMethod = encodings
                .Select(
                    encoding =>
                    {
                            var matchCount = profileCharacters
                                .Where(
                                    c =>
                                    {
                                        var charBytes = $"{c}".GetBytes(encoding);
                                        var rawDataSpan = rawData.AsSpan();
                                        var indexOfPattern = rawDataSpan.IndexOf(charBytes);
                                        return indexOfPattern >= 0;
                                    })
                                .Count();
                            return (encoding, matchCount);
                    })
                .Max(ep => ep.matchCount, ep => ep.encoding, () => default);
            return encodingProfileNewMethod;
        }

        public void WriteRows(string fileName, object[] rows)
        {
            // Note that the CSVHelper library expects the properties in the incoming object[] to be in the 
            // format of 
            //public class Foo
            //{
            //    public string Id { get; set; }
            //    public string Thing1 { get; set; }
            //}
            //The properties must be public and must have a getter and setter

            using (var textWriter = File.CreateText(fileName))
            using (var writer = new CsvWriter(textWriter, System.Globalization.CultureInfo.InvariantCulture))
            {
                writer.WriteRecords(rows);
            }
        }

        public void WriteRows<T>(IEnumerable<T> rows, bool leaveOpen = false)
        {
            // Note that the CSVHelper library expects the properties in the incoming object[] to be in the 
            // format of 
            //public class Foo
            //{
            //    public string Id { get; set; }
            //    public string Thing1 { get; set; }S
            //}
            //The properties must be public and must have a getter and setter
            using (var streamWriter = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen))
            using (var writer = new CsvWriter(streamWriter, System.Globalization.CultureInfo.InvariantCulture))
            {
                writer.WriteRecords(rows);
            }
        }
    }
}
