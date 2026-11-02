#nullable enable
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using EastFive.Configuration;

namespace EastFive.Security
{
    /// <summary>
    /// A configured symmetric key of an exact length, in the ONE encoding its member declares
    /// (base64, base64url or hex; never guessed). Declare the member with <see cref="SymmetricKeyAttribute"/>.
    /// The bytes are private; <see cref="ToArray"/> hands out a copy.
    /// </summary>
    public sealed class SymmetricKey
    {
        private readonly byte[] key;

        private SymmetricKey(byte[] key) => this.key = key;

        public int Length => key.Length;

        /// <summary>A copy of the key bytes; clear it when done.</summary>
        public byte[] ToArray() => (byte[])key.Clone();

        public override string ToString() => $"[{Length}-byte key]";

        public static string EncodingName(ConfigurationRandomEncoding encoding)
            => encoding switch
            {
                ConfigurationRandomEncoding.Base64 => "base64",
                ConfigurationRandomEncoding.Hex => "hex",
                _ => "base64url",
            };

        /// <summary>
        /// Text in <paramref name="encoding"/> to a key of exactly <paramref name="length"/> bytes, or
        /// the reason it is not one ("expected 32-byte key (base64); got 16 bytes"). Never echoes the text.
        /// </summary>
        public static TResult Parse<TResult>(string text, int length, ConfigurationRandomEncoding encoding,
            Func<SymmetricKey, TResult> onParsed,
            Func<string, TResult> onInvalid)
        {
            var expected = $"expected {length}-byte key ({EncodingName(encoding)})";
            if (!TryDecode(text.Trim(), encoding, out var bytes))
                return onInvalid($"{expected}; the value is not {EncodingName(encoding)}");
            return bytes.Length == length
                ? onParsed(new SymmetricKey(bytes))
                : onInvalid($"{expected}; got {bytes.Length} bytes");
        }

        private static bool TryDecode(string text, ConfigurationRandomEncoding encoding, out byte[] bytes)
        {
            switch (encoding)
            {
                case ConfigurationRandomEncoding.Base64:
                    return KeyText.TryFromBase64(text, out bytes);
                case ConfigurationRandomEncoding.Hex:
                    bytes = Array.Empty<byte>();
                    if (text.Length == 0 || text.Length % 2 != 0 || !text.All(char.IsAsciiHexDigit))
                        return false;
                    bytes = Convert.FromHexString(text);
                    return true;
                default:
                    return KeyText.TryFromBase64Url(text, out bytes);
            }
        }
    }
}
