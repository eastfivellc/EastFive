#nullable enable
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EastFive.Security
{
    /// <summary>RFC 7638 JWK thumbprints: SHA-256 over the key's required public members in
    /// lexicographic order, base64url. What a key id defaults to where we control registration.</summary>
    public static class JsonWebKeyThumbprint
    {
        /// <param name="n">The modulus, base64url.</param>
        /// <param name="e">The exponent, base64url.</param>
        public static string Rsa(string n, string e)
            => Hash($"{{\"e\":\"{e}\",\"kty\":\"RSA\",\"n\":\"{n}\"}}");

        /// <param name="crv">The JWK curve name (P-256, P-384, P-521).</param>
        public static string Ec(string crv, string x, string y)
            => Hash($"{{\"crv\":\"{crv}\",\"kty\":\"EC\",\"x\":\"{x}\",\"y\":\"{y}\"}}");

        private static string Hash(string canonical)
            => KeyText.Base64Url(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    /// <summary>
    /// How a key pair's text is recognised, in order: a PEM header is PEM; otherwise base64, where
    /// <c>&lt;RSAKeyValue</c> is the .NET XML and anything else is DER; otherwise JSON, a JWK.
    /// </summary>
    internal static class KeyText
    {
        public static string Base64Url(byte[] bytes)
            => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        public static bool TryFromBase64Url(string text, out byte[] bytes)
        {
            bytes = Array.Empty<byte>();
            var trimmed = text.TrimEnd('=');
            if (trimmed.Length == 0 || trimmed.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_')))
                return false;
            var standard = trimmed.Replace('-', '+').Replace('_', '/');
            if (standard.Length % 4 == 1)
                return false;
            return TryFromBase64(standard.PadRight(standard.Length + (4 - standard.Length % 4) % 4, '='), out bytes);
        }

        public static bool TryFromBase64(string text, out byte[] bytes)
        {
            var compact = string.Concat(text.Where(c => !char.IsWhiteSpace(c)));
            var buffer = new byte[compact.Length];
            if (compact.Length == 0 || !Convert.TryFromBase64String(compact, buffer, out var written))
            {
                bytes = Array.Empty<byte>();
                return false;
            }
            bytes = buffer[..written];
            return true;
        }

        public static bool IsPem(string text) => text.Contains("-----BEGIN ", StringComparison.Ordinal);

        /// <summary>PEM pasted into a single-line setting often arrives with literal <c>\n</c> escapes.</summary>
        public static string NormalizePem(string text)
            => text.Contains("\\n", StringComparison.Ordinal) ? text.Replace("\\r\\n", "\n").Replace("\\n", "\n") : text;

        /// <summary>The first PEM block's label (e.g. <c>PRIVATE KEY</c>), or null.</summary>
        public static string? PemLabel(string pem)
            => PemEncoding.TryFind(pem, out var fields) ? pem[fields.Label] : null;

        public static bool IsRsaXml(byte[] bytes)
        {
            var text = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
            return text.StartsWith("<RSAKeyValue", StringComparison.Ordinal);
        }

        public static string Xml(byte[] bytes) => Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');

        /// <summary>The text as a JSON object, or null when it is not one.</summary>
        public static JsonElement? JsonObject(string text)
        {
            if (!text.StartsWith('{'))
                return null;
            try
            {
                using var document = JsonDocument.Parse(text);
                return document.RootElement.ValueKind == JsonValueKind.Object ? document.RootElement.Clone() : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public static string? Member(JsonElement jwk, string name)
            => jwk.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        public static byte[]? Base64UrlMember(JsonElement jwk, string name)
            => Member(jwk, name) is { } text && TryFromBase64Url(text, out var bytes) ? bytes : null;

        /// <summary>Runs one import attempt; a refusal by the platform is "not this encoding", never an exception.</summary>
        public static bool Try(Action import)
        {
            try
            {
                import();
                return true;
            }
            catch (CryptographicException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }

    /// <summary>What reading key text found, before the member's constraints are applied.</summary>
    internal enum KeyReadOutcome
    {
        /// <summary>A private key of the expected family.</summary>
        Private,
        /// <summary>The expected family, but only its public half.</summary>
        PublicOnly,
        /// <summary>A recognised encoding the reader refuses (e.g. encrypted PEM); see the message.</summary>
        Refused,
        /// <summary>Not a key of the expected family in any accepted encoding.</summary>
        NotThisKind,
    }
}
