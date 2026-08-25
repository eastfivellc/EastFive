using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Reflection;

namespace EastFive.Serialization
{
    public static class StringExtensions
    {
        public static TResult BindTo<TResult>(this string content, Type type,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure)
        {
            if (type == typeof(string))
            {
                var stringValue = content;
                return onParsed((object)stringValue);
            }
            if (type == typeof(Guid))
            {
                if (Guid.TryParse(content, out Guid stringGuidValue))
                    return onParsed(stringGuidValue);
                return onBindingFailure($"Failed to convert `{content}` to type `{typeof(Guid).FullName}`.");
            }
            if (type == typeof(Guid[]))
            {
                if (content.IsNullOrWhiteSpace())
                    return onParsed(new Guid[] { });
                if (content.StartsWith('['))
                {
                    content = content
                        .TrimStart('[')
                        .TrimEnd(']');
                }
                var tokens = content.Split(','.AsArray());
                var guids = tokens
                    .Select(
                        token => BindTo(token, typeof(Guid),
                                    guid => guid,
                                    (why) => default(Guid?),
                                    (why) => default(Guid?)))
                    .Cast<Guid?>()
                    .Where(v => v.HasValue)
                    .Select(v => v.Value)
                    .ToArray();
                return onParsed(guids);
            }
            if (type == typeof(DateTime))
            {
                return ParseDate(content,
                    (currentDateString) => onDidNotBind(
                        $"Failed to convert {content} to `{typeof(DateTime).FullName}`."));

                TResult ParseDate(string dateString, Func<string, TResult> onParseFailed)
                {
                    if (dateString.IsNullOrWhiteSpace())
                        return onParseFailed(dateString);

                    if (DateTime.TryParse(dateString, out DateTime dateValue))
                        return onParsed(dateValue);

                    // Common format not supported by TryParse
                    if (DateTime.TryParseExact(dateString, "ddd MMM d yyyy HH:mm:ss 'GMT'K",
                            null, System.Globalization.DateTimeStyles.AllowWhiteSpaces, out dateValue))
                        return onParsed(dateValue);

                    var startOfDescText = dateString.IndexOf('(');
                    if (startOfDescText > 0)
                    {
                        var cleanerText = content.Substring(0, startOfDescText);
                        return ParseDate(cleanerText,
                            failedText =>
                            {
                                var decodedContent = System.Net.WebUtility.UrlDecode(failedText);
                                if (decodedContent != failedText)
                                    return ParseDate(decodedContent,
                                        (failedDecodedText) => onParseFailed(failedDecodedText));
                                return onParseFailed(failedText);
                            });
                    }

                    var decodedContent = System.Net.WebUtility.UrlDecode(dateString);
                    if (decodedContent != dateString)
                        return ParseDate(decodedContent,
                            (failedDecodedText) => onParseFailed(failedDecodedText));
                    return onParseFailed(dateString);
                }

            }
            if (type == typeof(DateTimeOffset))
            {
                if (DateTimeOffset.TryParse(content, out DateTimeOffset dateValue))
                    return onParsed(dateValue);
                return onDidNotBind($"Failed to convert {content} to `{typeof(DateTimeOffset).FullName}`.");
            }
            if (type == typeof(int))
            {
                if (int.TryParse(content, out int intValue))
                    return onParsed(intValue);
                return onBindingFailure($"Failed to convert {content} to `{typeof(int).FullName}`.");
            }
            if (type == typeof(long))
            {
                if (long.TryParse(content, out long longValue))
                    return onParsed(longValue);
                return onBindingFailure($"Failed to convert {content} to `{typeof(long).FullName}`.");
            }
            if (type == typeof(double))
            {
                if (double.TryParse(content, out double doubleValue))
                    return onParsed(doubleValue);
                return onBindingFailure($"Failed to convert {content} to `{typeof(double).FullName}`.");
            }
            if (type == typeof(decimal))
            {
                if (decimal.TryParse(content, out decimal decimalValue))
                    return onParsed(decimalValue);
                return onBindingFailure($"Failed to convert {content} to `{typeof(decimal).FullName}`.");
            }
            if (type == typeof(bool))
            {
                if (content.IsDefaultNullOrEmpty())
                    return onDidNotBind("Value not provided.");

                if (content.TryParseBool(out var boolValue))
                    return onParsed(boolValue);

                return onDidNotBind($"Failed to convert {content} to `{typeof(bool).FullName}`.");
            }
            if (type == typeof(Uri))
            {
                if (content.IsDefaultNullOrEmpty())
                    return onBindingFailure("URL value was empty");
                if (Uri.TryCreate(content.Trim('"'.AsArray()), UriKind.RelativeOrAbsolute, out Uri uriValue))
                    return onParsed(uriValue);
                return onBindingFailure($"Failed to convert {content} to `{typeof(Uri).FullName}`.");
            }
            if (type == typeof(Type))
            {
                return content.GetClrType(
                    typeInstance => onParsed(typeInstance),
                    () => onDidNotBind(
                        $"`{content}` is not a recognizable resource type or CLR type."));
                //() => HttpApplication.GetResourceType(content,
                //        (typeInstance) => onParsed(typeInstance),
                //        () => content.GetClrType(
                //            typeInstance => onParsed(typeInstance),
                //            () => onDidNotBind(
                //                $"`{content}` is not a recognizable resource type or CLR type."))));
            }
            if (type == typeof(Stream))
            {
                return BindTo(content, typeof(byte[]),
                    byteArrayValueObj =>
                    {
                        var byteArrayValue = (byte[])byteArrayValueObj;
                        return onParsed(new MemoryStream(byteArrayValue));
                    },
                    onDidNotBind,
                    onBindingFailure);
            }
            if (type == typeof(byte[]))
            {
                if (content.TryParseBase64String(out byte[] byteArrayValue))
                    return onParsed(byteArrayValue);
                return onDidNotBind($"Failed to convert {content} to `{typeof(byte[]).FullName}` as base64 string.");
            }
            if (type == typeof(object))
            {
                var objValue = content;
                return onParsed(objValue);
            }

            if (type.IsSubClassOfGeneric(typeof(IRef<>)))
                return BindTo(content, typeof(Guid),
                    (id) =>
                    {
                        var resourceType = type.GenericTypeArguments.First();
                        var instantiatableType = typeof(EastFive.Ref<>).MakeGenericType(resourceType);
                        var instance = Activator.CreateInstance(instantiatableType, new object[] { id });
                        return onParsed(instance);
                    },
                    onDidNotBind,
                    (why) => onBindingFailure(why));

            if (type.IsSubClassOfGeneric(typeof(IRefOptional<>)))
            {
                var referredType = type.GenericTypeArguments.First();

                TResult emptyOptional()
                {
                    var refInst = RefOptionalHelper.CreateEmpty(referredType);
                    return onParsed(refInst);
                };

                if (content.IsNullOrWhiteSpace())
                    return emptyOptional();
                if (content.ToLower() == "empty")
                    return emptyOptional();
                if (content.ToLower() == "null")
                    return emptyOptional();

                var refType = typeof(IRef<>).MakeGenericType(referredType);
                return BindTo(content, refType,
                    (v) =>
                    {
                        var refOptionalType = typeof(RefOptional<>).MakeGenericType(referredType);
                        var refInst = Activator.CreateInstance(refOptionalType, new object[] { v });
                        return onParsed(refInst);
                    },
                    (why) => emptyOptional(),
                    (why) => emptyOptional());
            }
            if (type.IsSubClassOfGeneric(typeof(IRefs<>)))
            {
                return BindTo(content, typeof(Guid[]),
                    (ids) =>
                    {
                        var resourceType = type.GenericTypeArguments.First();
                        var instantiatableType = typeof(Refs<>).MakeGenericType(resourceType);
                        var instance = Activator.CreateInstance(instantiatableType, new object[] { ids });
                        return onParsed(instance);
                    },
                    onDidNotBind,
                    (why) => onBindingFailure(why));
            }
            if (type.IsSubClassOfGeneric(typeof(Nullable<>)))
            {
                var underlyingType = type.GetNullableUnderlyingType();
                return BindTo(content, underlyingType,
                    (nonNullable) =>
                    {
                        var nullable = nonNullable.AsNullable();
                        return onParsed(nullable);
                    },
                    (why) => onParsed(type.GetDefault()),
                    (why) => onParsed(type.GetDefault()));
            }

            if (type.IsEnum)
            {
                if (Enum.TryParse(type, content, out object value))
                    return onParsed(value);

                var validValues = Enum.GetNames(type).Join(", ");
                return onDidNotBind($"Value `{content}` is not a valid value for `{type.FullName}.` Valid values are [{validValues}].");
            }

            if (type.IsArray)
            {
                var arrayType = type.GetElementType();
                var parsedAndBindedStringsTpl = content
                    .ParseStringToArray()
                    .Select(
                        value => BindTo(value, arrayType,
                            v => (0, v),
                            why => (1, why),
                            why => (2, why)))
                    .ToArray();

                return parsedAndBindedStringsTpl
                    .Where(tpl => tpl.Item1 != 0)
                    .First(
                        (tpl, next) => onBindingFailure((string)tpl.Item2),
                        () =>
                        {
                            var value = parsedAndBindedStringsTpl
                                .Select(tpl => tpl.Item2)
                                .ToArray()
                                .CastArray(arrayType);
                            return onParsed(value);
                        });
            }

            return onDidNotBind($"No binding for type `{type.FullName}` provided by {typeof(StringExtensions).FullName}..{nameof(BindTo)}");
        }

        public static string [] ParseStringToArray(this string content)
        {
            return content.MatchRegexInvoke(
                @"(\[(?<index>[0-9]+)\]=)(?<value>([^\;]|(?<=\\)\;)+)",
                (index, value) => index.PairWithValue(value),
                onMatched: tpls =>
                {
                    // either abc;def
                    // or [0]=abc;[1]=def
                    var matchesDictionary = tpls.Any(kvp => string.IsNullOrEmpty(kvp.Key))?
                            tpls
                                .Select(
                                    (kvp, index) => kvp.Value.PairWithKey(index))
                                .ToDictionary()
                        :
                            tpls
                                .TryWhere(
                                    (KeyValuePair<string, string> kvp, out int indexedValue) =>
                                        int.TryParse(kvp.Key, out indexedValue))
                                .Select(
                                    match => match.item.Value.PairWithKey(match.@out))
                                .ToDictionary();

                    if(matchesDictionary.IsDefaultNullOrEmpty())
                    {
                        return TryMatchJsonStyle();
                    }

                    // matchesDictionary.Keys will throw if empty
                    var extractedItems = Enumerable
                        .Range(0, matchesDictionary.Keys.Max() + 1)
                        .Select(
                            (index) =>
                            {
                                if (!matchesDictionary.TryGetValue(index, out string value))
                                    return (false, $"Missing index {index}");
                                return (true, value);
                            })
                        .SelectWhere()
                        .ToArray();

                    return extractedItems;

                    string[] TryMatchJsonStyle()
                    {
                        return content.MatchRegexInvoke(
                            @"\[(?<items>[^\[\]]+)\]",
                            (items) => items,
                            onMatched: tpls =>
                            {
                                return tpls
                                    .NullToEmpty()
                                    .Single(
                                        tpl => TryDelimited(tpl),
                                        onNoneOrMultiple: () => TryDelimited(content));
                            });
                    }

                    string[] TryDelimited(string delimitedContent)
                    {
                        if (delimitedContent.IsDefaultOrNull())
                            return new string[] { };
                        if (delimitedContent.Contains(';'))
                            return DoSplit(';');
                        if (delimitedContent.Contains(','))
                            return DoSplit(',');

                        return delimitedContent.AsArray();

                        string[] DoSplit(char delimiter)
                        {
                            var replacedDelimiter = Guid.NewGuid().ToString();
                            var escapedDelimiter = "\\" + delimiter;
                            var modifiedDelimitedContent = delimitedContent.Replace(escapedDelimiter, replacedDelimiter);
                            bool isModified = modifiedDelimitedContent.Length != delimitedContent.Length;
                            var values = isModified
                                ? modifiedDelimitedContent
                                    .Split(delimiter)
                                    .Select(v => v.Trim().Replace(replacedDelimiter, delimiter.ToString()))
                                    .ToArray()
                                : delimitedContent
                                    .Split(delimiter)
                                    .Select(v => v.Trim())
                                    .ToArray();

                            return Trim(new char[] { '\'', '"' });

                            string [] Trim(char[] trimChars)
                            {
                                if (trimChars.IsDefaultNullOrEmpty())
                                    return values;

                                var trimChar = trimChars.First();
                                var allValuesInQuotes = values
                                    .All(
                                        v =>
                                        {
                                            if (!v.StartsWith(trimChar))
                                                return false;
                                            if (!v.EndsWith(trimChar))
                                                return false;

                                            return true;
                                        });
                                if (allValuesInQuotes)
                                {
                                    var trimmedValues = values.Select(v => v.Trim(trimChar)).ToArray();
                                    return trimmedValues;
                                }

                                return Trim(trimChars.Skip(1).ToArray());
                            }
                        }
                    }
                });
                // return onDidNotBind($"Array not formatted correctly. It must be [0]=asdf;[1]=qwer;[2]=zxcv");
        }

        public static bool TryParseBool(this string boolStr, out bool boolValue)
        {
            boolValue = default;
            if (boolStr.IsDefaultNullOrEmpty())
                return false;

            var content = boolStr.ToLower().Trim();
            if ("t" == content)
            {
                boolValue = true;
                return true;
            }

            if ("on" == content) // used in check boxes
            {
                boolValue = true;
                return true;
            }

            if ("f" == content)
            {
                boolValue = false;
                return true;
            }

            if ("off" == content) // used in some check boxes
            {
                boolValue = false;
                return true;
            }

            if ("1" == content)
            {
                boolValue = true;
                return true;
            }

            if ("0" == content)
            {
                boolValue = false;
                return true;
            }

            // TryParse may convert "on" to false TODO: Test theory
            return bool.TryParse(content, out boolValue);
        }

        internal static T Decode<T>(string value)
        {
            value = value.Replace("-", "");
            if (String.IsNullOrWhiteSpace(value))
            {
                return default(T);
            }
            try
            {
                var settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Ignore, // prevents XXE attacks, such as Billion Laughs
                    MaxCharactersFromEntities = 1024,
                    XmlResolver = null,                   // prevents external entity DoS attacks, such as slow loading links or large file requests
                };

                using (var strReader = new StringReader(value))
                using (var xmlReader = XmlReader.Create(strReader, settings))
                {
                    var serializer = new DataContractSerializer(typeof(T));
                    T result = (T)serializer.ReadObject(xmlReader);
                    return result;
                }
            }
            catch (SerializationException)
            {
                return default(T);
            }
        }

        internal static string Encode<T>(T value)
        {
            if (EqualityComparer<T>.Default.Equals(value))
            {
                return String.Empty;
            }
            string serializedString;
            using (MemoryStream memoryStream = new MemoryStream())
            using (StreamReader reader = new StreamReader(memoryStream))
            {
                DataContractSerializer serializer = new DataContractSerializer(typeof(T));
                serializer.WriteObject(memoryStream, value);
                memoryStream.Position = 0;
                serializedString = reader.ReadToEnd();
            }
            return serializedString;
        }


    }
}
