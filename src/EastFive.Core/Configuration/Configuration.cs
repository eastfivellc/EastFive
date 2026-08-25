using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Web.Configuration
{
    public static class Settings
    {
        public static TResult GetString<TResult>(string key,
            Func<string, TResult> onFound,
            Func<string, TResult> onUnspecified)
        {
            return key.ConfigurationString(
                onFound,
                onUnspecified);
        }

        public static TResult GetUri<TResult>(string key,
            Func<Uri, TResult> onParsed,
            Func<string, TResult> unspecifiedOrInvalid)
        {
            return GetString(key,
                keyValue =>
                {
                    if (!Uri.TryCreate(keyValue, UriKind.RelativeOrAbsolute, out Uri uriValue))
                        return unspecifiedOrInvalid($"The configuration value for [{key}] is invalid. Please specify a URI value");
                    return onParsed(uriValue);
                },
                unspecifiedOrInvalid);
        }
        
        public static TResult GetDateTime<TResult>(string key,
            Func<DateTime, TResult> onParsed,
            Func<string, TResult> unspecifiedOrInvalid)
        {
            return GetString(key,
                keyValue =>
                {
                    if (!DateTime.TryParse(keyValue, out DateTime dateTimeValue))
                        return unspecifiedOrInvalid(
                            $"The configuration value for [{key}] is invalid. Please specify a valid date time");
                    var dateTimeValueUtc = dateTimeValue.ToUniversalTime();
                    return onParsed(dateTimeValueUtc);
                },
                unspecifiedOrInvalid);
        }

        public static TResult GetDouble<TResult>(string key,
            Func<double, TResult> onParsed,
            Func<string, TResult> unspecifiedOrInvalid)
        {
            return GetString(key,
                keyValue =>
                {
                    if (!double.TryParse(keyValue, out double doubleValue))
                        return unspecifiedOrInvalid(
                            $"The configuration value for [{key}] is invalid. Please specify a double value");
                    return onParsed(doubleValue);
                },
                unspecifiedOrInvalid);
        }

        public static TResult GetGuid<TResult>(string key,
            Func<Guid, TResult> onParsed,
            Func<string, TResult> unspecifiedOrInvalid)
        {
            return GetString(key,
                (keyValue) =>
                {
                    Guid guidValue;
                    if (!Guid.TryParse(keyValue, out guidValue))
                        return unspecifiedOrInvalid(
                            $"The configuration value for [{key}] is not in the correct format. Please specify a Guid value");
                    return onParsed(guidValue);
                },
                unspecifiedOrInvalid);
        }

        public static TResult GetBase64Bytes<TResult>(string key,
            Func<byte [], TResult> onParsed,
            Func<string, TResult> unspecifiedOrInvalid)
        {
            return GetString(key,
                keyValue =>
                {
                    try
                    {
                        var bytes = Convert.FromBase64String(keyValue);
                        return onParsed(bytes);
                    }
                    catch (Exception ex)
                    {
                        return unspecifiedOrInvalid($"Invalid base64 configuration value for [{key}]: {ex.Message}");
                    }
                },
                (whyString) => unspecifiedOrInvalid($"The configuration value for [{key}] is not specified. Please specify a Base64 value"));
        }
    }
}
