#nullable enable
using System;
using System.Globalization;

namespace EastFive.Configuration
{
    /// <summary>
    /// String-to-member conversion for the loader. Every config value originates as a string
    /// (appsettings, vault, a caller's <c>supply</c>), so this is the ONE place a member type is
    /// given meaning. Unsupported member types are rejected when the declaration is reflected,
    /// not when a value happens to arrive.
    /// </summary>
    public static class ConfigurationValueConverter
    {
        public static bool IsSupported(Type memberType)
        {
            var type = Nullable.GetUnderlyingType(memberType) ?? memberType;
            return type == typeof(string)
                || type == typeof(Secret)
                || type == typeof(Uri)
                || type == typeof(bool)
                || type == typeof(int)
                || type == typeof(long)
                || type == typeof(double)
                || type == typeof(decimal)
                || type == typeof(Guid)
                || type == typeof(TimeSpan)
                || type == typeof(DateTime)
                || type == typeof(DateTimeOffset)
                || type.IsEnum;
        }

        /// <summary>Converts trimmed text to <paramref name="memberType"/>; false when the text is not a
        /// valid instance. Never throws for a bad VALUE (the caller reports <see cref="ConfigIssueCategory.Invalid"/>).</summary>
        public static bool TryConvert(Type memberType, string text, out object? value)
        {
            var type = Nullable.GetUnderlyingType(memberType) ?? memberType;
            var culture = CultureInfo.InvariantCulture;
            value = null;

            if (type == typeof(string)) { value = text; return true; }
            if (type == typeof(Secret)) { value = new Secret(text); return true; }
            if (type == typeof(Uri)) return Assign(Uri.TryCreate(text, UriKind.Absolute, out var uri), uri, out value);
            if (type == typeof(bool)) return Assign(bool.TryParse(text, out var b), b, out value);
            if (type == typeof(int)) return Assign(int.TryParse(text, NumberStyles.Integer, culture, out var i), i, out value);
            if (type == typeof(long)) return Assign(long.TryParse(text, NumberStyles.Integer, culture, out var l), l, out value);
            if (type == typeof(double)) return Assign(double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, culture, out var d), d, out value);
            if (type == typeof(decimal)) return Assign(decimal.TryParse(text, NumberStyles.Number, culture, out var m), m, out value);
            if (type == typeof(Guid)) return Assign(Guid.TryParse(text, out var g), g, out value);
            if (type == typeof(TimeSpan)) return Assign(TimeSpan.TryParse(text, culture, out var ts), ts, out value);
            if (type == typeof(DateTime)) return Assign(DateTime.TryParse(text, culture, DateTimeStyles.RoundtripKind, out var dt), dt, out value);
            if (type == typeof(DateTimeOffset)) return Assign(DateTimeOffset.TryParse(text, culture, DateTimeStyles.RoundtripKind, out var dto), dto, out value);
            if (type.IsEnum) return Assign(Enum.TryParse(type, text, ignoreCase: true, out var e) && Enum.IsDefined(type, e!), e, out value);

            throw new InvalidOperationException($"Configuration members of type {memberType.Name} are not supported.");
        }

        private static bool Assign<T>(bool parsed, T parsedValue, out object? value)
        {
            value = parsed ? parsedValue : null;
            return parsed;
        }
    }
}
