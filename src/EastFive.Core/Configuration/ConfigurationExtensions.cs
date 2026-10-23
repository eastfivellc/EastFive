using EastFive.Configuration;
using EastFive.Extensions;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Web.Configuration
{
    public static class ConfigurationExtensions
    {
        private static IConfiguration configuration;

        public static void Initialize(IConfiguration configuration)
        {
            ConfigurationExtensions.configuration = configuration;
        }

        /// <summary>Every key the ambient configuration holds (empty before <see cref="Initialize"/>) —
        /// how the catalog enumerates the instances of keyed configuration types.</summary>
        public static IEnumerable<string> ConfiguredKeys()
            => configuration == null
                ? Enumerable.Empty<string>()
                : Microsoft.Extensions.Configuration.ConfigurationExtensions.AsEnumerable(configuration)
                    .Where(pair => pair.Value != null)
                    .Select(pair => pair.Key)
                    .ToArray();

        /// <summary>
        /// A key that holds a STRUCTURED section rather than a value (an <c>appsettings.json</c> array or
        /// object), as JSON text: integer-keyed children become an array, others an object, leaves
        /// strings. Null when the key has no children. How a typed member reads a JSON setting that is
        /// written structurally — the raw JSON reader accepted both spellings.
        /// </summary>
        internal static string SectionJson(string key)
        {
            if (configuration == null)
                return null;
            var section = configuration.GetSection(key);
            return section.GetChildren().Any()
                ? Newtonsoft.Json.JsonConvert.SerializeObject(ToJson(section))
                : null;

            static object ToJson(IConfigurationSection node)
            {
                var children = node.GetChildren().ToArray();
                if (children.Length == 0)
                    return node.Value;
                return children.All(child => int.TryParse(child.Key, out _))
                    ? children.OrderBy(child => int.Parse(child.Key)).Select(ToJson).ToArray()
                    : children.ToDictionary(child => child.Key, ToJson);
            }
        }

        public static TResult ConfigurationString<TResult>(this string key,
            Func<string, TResult> onFound,
            Func<string, TResult> onUnspecified = default)
        {
            var value = configuration[key];
            if (!value.IsDefaultOrNull())
                return onFound(value);

            var msg = $" - The configuration value for [{key}] is not specified. Please specify a string value";
            if (!onUnspecified.IsDefault())
                return onUnspecified(msg);
            throw new ConfigurationException(key, typeof(string), msg);
        }

        public static TResult ConfigurationString<TResult>(this IProvideConfigurationKey key,
            Func<string, TResult> onFound,
            Func<string, TResult> onUnspecified = default)
        {
            if (key.IsDefaultOrNull())
                throw new ArgumentException("Configuration Key is null");
            var value = configuration[key.Key];
            if (!value.IsDefaultOrNull())
                return onFound(value);

            var msg = $" - The configuration value for [{key}] is not specified. Please specify a string value";
            if (!onUnspecified.IsDefault())
                return onUnspecified(msg);
            throw new ConfigurationException(key.Key, typeof(string), msg);
        }

        private static TResult ConfigurationBase<TBase, TResult>(string key,
                Func<string, Func<string, TResult>, TResult> convert,
            Func<string, TResult> onFailure = default,
            Func<TResult> onNotSpecified = default)
        {
            return ConfigurationString(key,
                keyValue =>
                {
                    return convert(keyValue,
                        (why) =>
                        {
                            var msg = $"The configuration value for [{key}] is invalid:{why}.\nPlease specify a valid {typeof(TBase).Name} value.";

                            if (!onFailure.IsDefault())
                                return onFailure(msg);

                            throw new ConfigurationException(key, typeof(TBase), msg);
                        });
                },
                onUnspecified:(why) =>
                {
                    if (!onNotSpecified.IsDefault())
                        return onNotSpecified();

                    var msg = $" - The configuration value for [{key}] is not specified. Please specify a {typeof(TBase).Name} value.";
                    if (!onFailure.IsDefault())
                        return onFailure(msg);

                    throw new ConfigurationException(key, typeof(TBase), msg);
                });
        }

        public static TResult ConfigurationBoolean<TResult>(this string key,
            Func<bool, TResult> onParsed,
            Func<string, TResult> onFailure = default,
            Func<TResult> onNotSpecified = default)
        {
            return ConfigurationBase<bool, TResult>(key,
                (keyValue, failureCallback) =>
                {
                    if (bool.TryParse(keyValue, out bool boolValue))
                        return onParsed(boolValue);

                    return failureCallback("Could not evaluate bool.");
                },
                onFailure: onFailure,
                onNotSpecified: onNotSpecified);
        }

        public static Uri ConfigurationUri(this string key)
            => key.ConfigurationUri(v => v);

        public static TResult ConfigurationUri<TResult>(this string key,
            Func<Uri, TResult> onParsed,
            Func<string, TResult> onFailure = default,
            Func<TResult> onNotSpecified = default)
        {
            return ConfigurationBase<Uri, TResult>(key,
                (keyValue, failureCallback) =>
                {
                    if (Uri.TryCreate(keyValue, UriKind.RelativeOrAbsolute, out Uri uriValue))
                        return onParsed(uriValue);

                    return failureCallback("Could not evaluate URI.");
                },
                onFailure: onFailure,
                onNotSpecified: onNotSpecified);
        }

        public static TResult ConfigurationDateTime<TResult>(this string key,
            Func<DateTime, TResult> onParsed,
            Func<string, TResult> onFailure = default,
            Func<TResult> onNotSpecified = default)
        {
            return ConfigurationBase<DateTime, TResult>(key,
                (keyValue, failureCallback) =>
                {
                    if (DateTime.TryParse(keyValue, out DateTime dateTimeValue))
                    {
                        var dateTimeValueUtc = dateTimeValue.ToUniversalTime();
                        return onParsed(dateTimeValueUtc);
                    }

                    return failureCallback("Could not evaluate DateTime.");
                },
                onFailure: onFailure,
                onNotSpecified: onNotSpecified);
        }

        public static TResult ConfigurationDouble<TResult>(this string key,
            Func<double, TResult> onParsed,
            Func<string, TResult> onFailure = default,
            Func<TResult> onNotSpecified = default)
        {
            return ConfigurationBase<double, TResult>(key,
                (keyValue, failureCallback) =>
                {
                    if (double.TryParse(keyValue, out double doubleValue))
                        return onParsed(doubleValue);

                    return failureCallback("Could not evaluate double.");
                },
                onFailure: onFailure,
                onNotSpecified: onNotSpecified);
        }

        public static TResult ConfigurationLong<TResult>(this string key,
            Func<long, TResult> onParsed,
            Func<string, TResult> onFailure = default,
            Func<TResult> onNotSpecified = default)
        {
            return ConfigurationBase<long, TResult>(key,
                (keyValue, failureCallback) =>
                {
                    if (long.TryParse(keyValue, out long longValue))
                        return onParsed(longValue);

                    return failureCallback("Could not evaluate double.");
                },
                onFailure: onFailure,
                onNotSpecified: onNotSpecified);
        }

        public static TResult ConfigurationGuid<TResult>(this string key,
            Func<Guid, TResult> onParsed,
            Func<string, TResult> onFailure = default,
            Func<TResult> onNotSpecified = default)
        {
            return ConfigurationBase<Guid, TResult>(key,
                (keyValue, failureCallback) =>
                {
                    if (Guid.TryParse(keyValue, out Guid guidValue))
                        return onParsed(guidValue);

                    return failureCallback("Could not evaluate Guid.");
                },
                onFailure: onFailure,
                onNotSpecified: onNotSpecified);
        }

        public static TResult ConfigurationBase64Bytes<TResult>(this string key,
            Func<byte[], TResult> onParsed,
            Func<string, TResult> onFailure = default,
            Func<TResult> onNotSpecified = default)
        {
            return ConfigurationBase<byte[], TResult>(key,
                (keyValue, failureCallback) =>
                {
                    if (keyValue == null)
                        return onNotSpecified();
                    try
                    {
                        var bytes = Convert.FromBase64String(keyValue);
                        return onParsed(bytes);
                    }
                    catch (FormatException ex)
                    {
                        return failureCallback(ex.Message);
                    }
                },
                onFailure: onFailure,
                onNotSpecified: onNotSpecified);
        }

        public static TResult ConfigurationJson<TConfig, TResult>(this string key,
            Func<TConfig, TResult> onParsed,
            Func<string, TResult> onFailure = default,
            Func<TResult> onNotSpecified = default)
        {
            try
            {
                var keyValue = GetJson();
                var obj = Newtonsoft.Json.JsonConvert.DeserializeObject<TConfig>(keyValue);
                return onParsed(obj);
            } catch(Exception ex)
            {
                return onFailure(ex.Message);
            }

            string GetJson()
            {
                if (typeof(TConfig).IsArray)
                {
                    var dictArray = configuration
                        .GetSection(key)
                        .Get<Dictionary<string, object>[]>();
                    if (dictArray != null)
                        return Newtonsoft.Json.JsonConvert.SerializeObject(dictArray);

                    return configuration[key];
                }

                var dict = configuration
                    .GetSection(key)
                    .Get<Dictionary<string, object>[]>();
                if (dict != null)
                    return Newtonsoft.Json.JsonConvert.SerializeObject(dict);

                return configuration[key];
            }
        }
    }
}
