#nullable enable
using System;
using System.Text;
using System.Text.RegularExpressions;

namespace EastFive.Configuration
{
    /// <summary>
    /// The general secret-name translation, for an id whose format is not ours to choose (a vendor's
    /// webhook id): ANY non-empty text becomes a valid segment, and two different ids never share
    /// one. Opt-in, on an account member or a keyed type's
    /// <see cref="IProvideConfigurationInstanceKey.InstanceKey"/>; without it an id that is not a
    /// segment is still refused.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>A PLAIN segment — letters and digits in groups joined by single dashes
    /// (<c>acme-prod</c>) — is returned unchanged, so ids that never needed a translation keep the
    /// names they have.</item>
    /// <item>Anything else becomes <c>e</c> followed by each character: an ASCII letter or digit as
    /// itself, every other character (each dash included) as <c>--</c> and two lowercase hex digits
    /// per UTF-8 byte. <c>wh_abc</c> is <c>ewh--5fabc</c>; <c>_a</c> is <c>e--5fa</c>.</item>
    /// </list>
    /// A plain segment never contains <c>--</c> and an escaped one always does, so the two forms
    /// cannot meet, and <see cref="TryDecode"/> reads either back. Vault secret names ignore letter
    /// case, and so does this: the only ids that share a name are ones differing by ASCII letter case
    /// alone. Text with no UTF-8 form (a lone surrogate) is returned as it is, and so refused.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class EncodedSecretAccountSegmentAttribute : Attribute, IProvideSecretAccountSegment
    {
        public const char Marker = 'e';
        public const string Escape = "--";

        // \z, not $: a trailing newline is part of the value and must be escaped, not ignored.
        private static readonly Regex Plain = new(@"\A[A-Za-z0-9]+(?:-[A-Za-z0-9]+)*\z", RegexOptions.CultureInvariant);
        private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        public string SecretAccountSegment(string account) => Encode(account);

        /// <summary><paramref name="value"/> as a secret-name segment: itself when it is a plain
        /// segment (or empty, or not encodable, which stay invalid), otherwise its escaped form.</summary>
        public static string Encode(string value)
        {
            if (string.IsNullOrEmpty(value) || Plain.IsMatch(value))
                return value;
            byte[] bytes;
            try
            {
                bytes = Utf8.GetBytes(value);
            }
            catch (EncoderFallbackException)
            {
                return value;
            }
            var segment = new StringBuilder(value.Length + 8).Append(Marker);
            foreach (var octet in bytes)
            {
                if (IsLetterOrDigit(octet))
                    segment.Append((char)octet);
                else
                    segment.Append(Escape).Append(Hex(octet >> 4)).Append(Hex(octet & 0xF));
            }
            return segment.ToString();
        }

        /// <summary>The value <see cref="Encode"/> turned into <paramref name="segment"/>; false for
        /// text it never produces (so a decoded value always encodes back to the same segment).</summary>
        public static bool TryDecode(string segment, out string value)
        {
            value = string.Empty;
            if (string.IsNullOrEmpty(segment))
                return false;
            if (!segment.Contains(Escape, StringComparison.Ordinal))
            {
                value = segment;
                return Plain.IsMatch(segment);
            }
            if (segment[0] != Marker)
                return false;

            var bytes = new byte[segment.Length];
            var count = 0;
            for (var index = 1; index < segment.Length; index++)
            {
                var character = segment[index];
                if (character < 128 && IsLetterOrDigit((byte)character))
                {
                    bytes[count++] = (byte)character;
                    continue;
                }
                if (character != '-' || index + 3 >= segment.Length || segment[index + 1] != '-'
                    || !TryHex(segment[index + 2], out var high) || !TryHex(segment[index + 3], out var low))
                    return false;
                bytes[count++] = (byte)((high << 4) | low);
                index += 3;
            }

            string decoded;
            try
            {
                decoded = Utf8.GetString(bytes, 0, count);
            }
            catch (DecoderFallbackException)
            {
                return false;
            }
            if (!string.Equals(Encode(decoded), segment, StringComparison.Ordinal))
                return false;
            value = decoded;
            return true;
        }

        private static bool IsLetterOrDigit(byte octet)
            => octet is (>= (byte)'A' and <= (byte)'Z') or (>= (byte)'a' and <= (byte)'z') or (>= (byte)'0' and <= (byte)'9');

        private static char Hex(int nibble) => (char)(nibble < 10 ? '0' + nibble : 'a' + nibble - 10);

        private static bool TryHex(char character, out int nibble)
        {
            nibble = character is >= '0' and <= '9' ? character - '0'
                : character is >= 'a' and <= 'f' ? character - 'a' + 10
                : -1;
            return nibble >= 0;
        }
    }
}
