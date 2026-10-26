using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EastFive.Web.Configuration
{
    /// <summary>
    /// Installs the ambient configuration a host built (<see cref="Initialize"/>). There is no public
    /// read: configuration is read through a typed <see cref="EastFive.Configuration.IProvideConfiguration"/>
    /// loaded by <see cref="EastFive.Configuration.ConfigurationLoader"/>, and the raw reads below are
    /// internal to the loader.
    /// </summary>
    public static class ConfigurationExtensions
    {
        private static IConfiguration configuration;

        public static void Initialize(IConfiguration configuration)
        {
            ConfigurationExtensions.configuration = configuration;
        }

        /// <summary>Every key the ambient configuration holds (empty before <see cref="Initialize"/>) —
        /// how the catalog enumerates the instances of keyed configuration types.</summary>
        internal static IEnumerable<string> ConfiguredKeys()
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
        /// written structurally rather than as one JSON string.
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

        /// <summary>The ambient value of <paramref name="key"/>, or null — the read under
        /// <see cref="EastFive.Configuration.ConfigurationLoader.ReadAmbient"/>. Throws when
        /// configuration is not initialized, which the loader reports as a source failure.</summary>
        internal static string ReadValue(string key) => configuration[key];
    }
}
