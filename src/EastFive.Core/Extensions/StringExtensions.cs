using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using EastFive.Linq;
using EastFive.Serialization;

namespace EastFive
{
    public static class StringExtensions
    {
        public static TResult TrimEnd<TResult>(this string str, string trim,
            Func<string, TResult> success, Func<string, TResult> doesNotEndWithTrim)
        {
            if (!str.EndsWith(trim))
                return doesNotEndWithTrim(str);
            var trimmed = str.Substring(0, str.Length - trim.Length);
            return success(trimmed);
        }

        public static string Format(this string str, params object[] args)
        {
            return String.Format(str, args);
        }

        public static string Join(this IEnumerable<string> strings, string separator)
        {
            return String.Join(separator, strings.NullToEmpty());
        }

        public static string Join(this IEnumerable<string> strings, char separator)
        {
            return String.Join(separator.ToString(), strings.NullToEmpty());
        }

        public static string Join(this IEnumerable<char> chars)
        {
#if NETCOREAPP2_1_OR_GREATER
            return String.Concat(chars);
#else
            var sb = new System.Text.StringBuilder();
            foreach (var c in chars)
            {
                sb.Append(c);
            }

            return sb.ToString();
#endif
        }

        public static string Base64(this string value, System.Text.Encoding encoding = default(System.Text.Encoding))
        {
#region per https://tools.ietf.org/html/rfc4648#section-10 null/empty string are encoded as ""
            if (value.IsNullOrEmpty())
                return string.Empty;
#endregion

            if (default(System.Text.Encoding) == encoding)
                encoding = System.Text.Encoding.UTF8;
            var bytes = encoding.GetBytes(value);
            return Convert.ToBase64String(bytes);
        }

        public static byte[] FromBase64String(this string base64String)
        {
            string incoming = base64String
                   .Replace('_', '/')
                   .Replace('-', '+');
            switch (base64String.Length % 4)
            {
                case 2: incoming += "=="; break;
                case 3: incoming += "="; break;
            }
            return Convert.FromBase64String(incoming);
        }

        public static bool TryParseBase64String(this string base64String, out byte[] bytes)
        {
            string incoming = base64String
                .Replace('_', '/')
                .Replace('-', '+');
            switch (base64String.Length % 4)
            {
                case 2: incoming += "=="; break;
                case 3: incoming += "="; break;
            }
            try
            {
                bytes = Convert.FromBase64String(incoming);
                return true;
            }catch(Exception)
            {
                bytes = default;
                return false;
            }
        }


        public static string ToBase64String(this byte[] bytes, bool urlSafe = false)
        {
            var encoding = Convert.ToBase64String(bytes);
            if (!urlSafe)
                return encoding;
            return encoding
                .Replace('+', '-')
                .Replace('/', '_')
                .Replace("=", "");
        }

        public static TResult GetClrType<TResult>(this string type,
            Func<Type, TResult> matched,
            Func<TResult> noMatch)
        {
            if (type.TryGetClrType(out Type clrType))
                return matched(clrType);
            return noMatch();
        }

        public static Type GetClrType(this string type)
        {
            return GetClrType(type,
                matchedType => matchedType,
                () =>
                {
                    throw new InvalidDataException($"Type {type} not supported");
                });
        }

        public static bool TryGetClrType(this string type, out Type clrType)
        {
            if (type.IsNullOrWhiteSpace())
            {
                clrType = default;
                return false;
            }
            if (type.ToLower() == "string")
            {
                clrType = typeof(string);
                return true;
            }
            if (type.ToLower() == "int")
            {
                clrType = typeof(int);
                return true;
            }
            if (type.ToLower() == "count")
            {
                clrType = typeof(int);
                return true;
            }
            if (type.ToLower() == "number")
            {
                clrType = typeof(decimal);
                return true;
            }
            if (type.ToLower() == "decimal")
            {
                clrType = typeof(decimal);
                return true;
            }
            if (type.ToLower() == "double")
            {
                clrType = typeof(double);
                return true;
            }
            if (type.ToLower() == "long")
            {
                clrType = typeof(long);
                return true;
            }
            if (type.ToLower() == "single")
            {
                clrType = typeof(float);
                return true;
            }
            if (type.ToLower() == "integer")
            {
                clrType = typeof(int);
                return true;
            }
            if (type.ToLower() == "int32")
            {
                clrType = typeof(int);
                return true;
            }
            if (type.ToLower() == "bool")
            {
                clrType = typeof(bool);
                return true;
            }
            if (type.ToLower() == "boolean")
            {
                clrType = typeof(bool);
                return true;
            }
            if (type.ToLower() == "text")
            {
                clrType = typeof(string);
                return true;
            }
            clrType = default;
            return false;
        }

        public static string GetClrString(this Type type)
        {
            if(type.IsNullable())
            {
                var tryUnderlyingType = type
                    .GetNullableUnderlyingType();
                if (tryUnderlyingType != null)
                {
                    var baseTypeClrString = tryUnderlyingType
                        .GetClrString();
                    return $"{baseTypeClrString}?";
                }
            }
            if (type == typeof(string))
                return "string";
            if (type == typeof(int))
                return "int";
            if (type == typeof(long))
                return "int";
            if (type == typeof(Int32))
                return "int";
            if (type == typeof(decimal))
                return "number";
            if (type == typeof(double))
                return "number";
            if (type == typeof(float))
                return "number";
            if (type == typeof(bool))
                return "boolean";
            return type.AssemblyQualifiedName;
            //throw new InvalidDataException($"Type {type} not supported");
        }

        public static string EscapeSingleQuote(this string parameter)
        {
            var trimmed = parameter.ToString()
                .Trim('\'');
            var characterScoped = new string(trimmed
                .Where(c => c > 26)
                .ToArray());
            return characterScoped
                .Replace("\\", "\\\\")
                .Replace("\'", "\'\'");
        }

        public static string ToHypenCase(this string source)
        {
            return source
                .NullToEmpty()
                .Select(
                    c =>
                    {
                        if (char.IsUpper(c))
                            return $"-{c}";
                        return c.ToString();
                    })
                .Join("");
        }

        public static bool IsGuid(this string possibleGuid)
        {
            Guid discard;
            return Guid.TryParse(possibleGuid, out discard);
        }

        public static bool IsGuids(this string possibleGuids, out Guid[] values)
        {
            if (possibleGuids.IsNullOrWhiteSpace())
            {
                values = new Guid[] { };
                return false;
            }

            var possibleGuidArray = possibleGuids.Split(',', StringSplitOptions.TrimEntries);
            values = possibleGuidArray
                .Select(possibleGuid =>
                {
                    return Guid.TryParse(possibleGuid, out Guid guid)
                        ? guid
                        : default(Guid?);
                })
                .SelectWhereHasValue()
                .ToArray();
            return values.Any();
        }

        public static string ToText(this byte [] bytes, System.Text.Encoding encoding = default(System.Text.Encoding))
        {
            if (default(System.Text.Encoding) == encoding)
                encoding = System.Text.ASCIIEncoding.ASCII;
            return encoding.GetString(bytes);
        }

        public static Stream ToStream(this string text, System.Text.Encoding encoding = default(System.Text.Encoding))
        {
            var bytes = text.GetBytes(encoding);
            var stream = new MemoryStream(bytes);
            return stream;
        }

#nullable enable

        public static bool IsNullOrEmpty([NotNullWhen(false)] this string? value)
        {
            return String.IsNullOrEmpty(value);
        }

        public static bool IsNullOrWhiteSpace([NotNullWhen(false)] this string? value)
        {
            return String.IsNullOrWhiteSpace(value);
        }

        public static bool HasBlackSpace([NotNullWhen(true)] this string? value)
        {
            return !String.IsNullOrWhiteSpace(value);
        }

#nullable restore

        public static string NullToEmpty(this string value)
        {
            if (value is null)
                return string.Empty;
            return value;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="value"></param>
        /// <param name="onIsNullOrWhiteSpace"></param>
        /// <param name="onHasContent">Passes back the string that was passed in to help keep the calling method from getting the params backwards</param>
        /// <returns></returns>
        public static TResult IsNullOrWhiteSpace<TResult>(this string value,
            Func<TResult> onIsNullOrWhiteSpace,
            Func<string, TResult> onHasContent)
        {
            return String.IsNullOrWhiteSpace(value) ?
                onIsNullOrWhiteSpace()
                :
                onHasContent(value);
        }

        public static string RemoveWhitespace(this string input)
        {
            if (input.IsNullOrEmpty())
                return input;

            if (input.Length < 100)
            {
                var charactersNoWhitespace = input
                    .Where(c => !Char.IsWhiteSpace(c))
                    .ToArray();
                return new string(charactersNoWhitespace);
            }

            var whiteSpaceRegex = new Regex(@"\s+");
            return whiteSpaceRegex.Replace(input, string.Empty);
        }

        public static bool EqualsTrimmed(this string str1, string str2)
        {
            if (str1.IsNullOrWhiteSpace())
                return str2.IsNullOrWhiteSpace();

            return String.Equals(str1.Trim(), str2.Trim());
        }

        public static TEnum AsEnum<TEnum>(this string value)
            where TEnum : struct // best we can do for now
        {
            return (TEnum)Enum.Parse(typeof(TEnum), value);
        }

        public static string ToLowerNullSafe(this string value)
        {
            if (value is null)
                return value;
            return value.ToLower();
        }

        public static string ToUpperNullSafe(this string value)
        {
            if (value is null)
                return value;
            return value.ToUpper();
        }

        public static System.Security.SecureString AsReadOnlySecureString(this string stringValue)
        {
            var secureString = new System.Security.SecureString();
            foreach (var c in stringValue)
                secureString.AppendChar(c);
            secureString.MakeReadOnly();
            return secureString;
        }
    }
}