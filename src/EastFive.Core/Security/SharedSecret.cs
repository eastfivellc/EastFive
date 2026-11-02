#nullable enable
using System;
using System.Security.Cryptography;
using System.Text;

namespace EastFive.Security
{
    /// <summary>
    /// A configured shared secret we issue (a webhook query secret, a host-to-host bearer): any text,
    /// used as its UTF-8 bytes, at least a minimum length. Gates compare with <see cref="Matches"/>;
    /// only a consumer that must SEND the value (a callback URL, a vendor setting) calls
    /// <see cref="Reveal"/>. Has no public property, so serializers see an empty object.
    /// </summary>
    public sealed class SharedSecret
    {
        private readonly string value;

        private SharedSecret(string value) => this.value = value;

        /// <summary>Constant-time comparison: both sides are hashed to a fixed length first, so neither
        /// the content nor the length of the secret leaks through timing.</summary>
        public bool Matches(string? presented)
        {
            if (string.IsNullOrEmpty(presented))
                return false;
            return CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(presented)),
                SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        }

        /// <summary>The value, for a consumer that must send it. Never log or store the result.</summary>
        public string Reveal() => value;

        /// <summary>The value's UTF-8 bytes (e.g. key-derivation input); a copy.</summary>
        public byte[] Utf8Bytes() => Encoding.UTF8.GetBytes(value);

        public override string ToString() => "[secret]";

        /// <summary>Text to a secret of at least <paramref name="minLength"/> UTF-8 bytes, or the reason it is not one.</summary>
        public static TResult Parse<TResult>(string text, int minLength,
            Func<SharedSecret, TResult> onParsed,
            Func<string, TResult> onInvalid)
        {
            var length = Encoding.UTF8.GetByteCount(text);
            return length < minLength
                ? onInvalid($"expected a shared secret of at least {minLength} bytes; got {length}")
                : onParsed(new SharedSecret(text));
        }
    }
}
