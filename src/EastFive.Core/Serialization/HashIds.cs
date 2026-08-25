// Adapted from Markus Ullmark
// https://github.com/ullmark/hashids.net/blob/master/src/Hashids.net/Hashids.cs

using System.Collections.Generic;
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Buffers;
using EastFive.Extensions;
using EastFive.Linq;

namespace EastFive.Serialization
{
    /// <summary>
    /// Generate YouTube-like hashes from one or many numbers. Use hashids when you do not want to expose your database ids to the user.
    /// </summary>
    public static class HashIdExtensions
    {
        /// <summary>
        /// Encodes the provided numbers into a hash string.
        /// </summary>
        /// <param name="numbers">List of integers.</param>
        /// <returns>Encoded hash string.</returns>
        public static string EncodeHashId(this IEnumerable<int> numbers, HashIdOptions options = default)
        {
            if (options.IsDefaultOrNull())
                options = new HashIdOptions();
            var longs = numbers.Select(n => (long)n).ToArray();
            return GenerateHashFrom(longs, options);
        }

        /// <summary>
        /// Encodes the provided numbers into a hash string.
        /// </summary>
        /// <param name="numbers">List of 64-bit integers.</param>
        /// <returns>Encoded hash string.</returns>
        public static string EncodeHashId(this IEnumerable<long> numbers, HashIdOptions options = default)
        {
            if (options.IsDefaultOrNull())
                options = new HashIdOptions();
            return GenerateHashFrom(numbers.ToArray(), options);
        }

        /// <summary>
        /// Encodes the provided numbers into a hash string.
        /// </summary>
        /// <param name="numbers">List of 64-bit integers.</param>
        /// <returns>Encoded hash string.</returns>
        public static string EncodeHashId(this IEnumerable<ulong> numbers, HashIdOptions options = default)
        {
            if (options.IsDefaultOrNull())
                options = new HashIdOptions();
            return GenerateHashFrom(numbers.ToArray(), options);
        }

        /// <summary>
        /// Decodes the provided hash into numbers.
        /// </summary>
        /// <param name="hash">Hash string to decode.</param>
        /// <returns>Array of integers.</returns>
        /// <exception cref="T:System.OverflowException">If the decoded number overflows integer.</exception>
        public static int[] DecodeHashIdInts(this string hash, HashIdOptions options = default)
        {
            if (options.IsDefaultOrNull())
                options = new HashIdOptions();
            var x = GetNumbersFrom(hash, options: options);
            return Array.ConvertAll(x, n => (int)n);
        }

        /// <summary>
        /// Decodes the provided hash into numbers.
        /// </summary>
        /// <param name="hash">Hash string to decode.</param>
        /// <returns>Array of 64-bit integers.</returns>
        public static long[] DecodeHashIdLongs(this string hash, HashIdOptions options = default)
        {
            if (options.IsDefaultOrNull())
                options = new HashIdOptions();
            return GetNumbersFrom(hash, options);
        }

        // Creates the Regex in the first usage, speed up first use of non-hex methods
        private static readonly Lazy<Regex> hexValidator = new(() => new Regex("^[0-9a-fA-F]+$", RegexOptions.Compiled));
        private static readonly Lazy<Regex> hexSplitter = new(() => new Regex(@"[\w\W]{1,12}", RegexOptions.Compiled));

        /// <summary>
        /// Encodes the provided hex-string into a hash string.
        /// </summary>
        /// <param name="hex">Hex string to encode.</param>
        /// <returns>Encoded hash string.</returns>
        public static string EncodeHashIdHex(this string hex, HashIdOptions options = default)
        {
            if (options.IsDefaultOrNull())
                options = new HashIdOptions();

            if (!hexValidator.Value.IsMatch(hex))
                return string.Empty;

            var matches = hexSplitter.Value.Matches(hex);
            var numbers = new List<long>(matches.Count);

            foreach (Match match in matches)
            {
                var number = Convert.ToInt64(string.Concat("1", match.Value), 16);
                numbers.Add(number);
            }

            return numbers.ToArray().EncodeHashId(options:options);
        }

        /// <summary>
        /// Encodes the provided hex-string into a hash string.
        /// </summary>
        /// <param name="hex">Hex string to encode.</param>
        /// <returns>Encoded hash string.</returns>
        public static string EncodeHashId(this string text, HashIdOptions options = default)
        {
            return text
                .GetBytes(Encoding.UTF8)
                .Split(last => 8)
                .Select(bytes => Convert.ToUInt64(bytes))
                .EncodeHashId(options: options);
        }

        /// <summary>
        /// Decodes the provided hash into a hex-string.
        /// </summary>
        /// <param name="hash">Hash string to decode.</param>
        /// <returns>Decoded hex string.</returns>
        public static string DecodeHex(string hash, HashIdOptions options = default)
        {
            var builder = new StringBuilder();
            var numbers = hash.DecodeHashIdLongs(options:options);

            foreach (var number in numbers)
            {
                var s = number.ToString("X");

                for (var i = 1; i < s.Length; i++)
                {
                    builder.Append(s[i]);
                }
            }

            var result = builder.ToString();
            return result;
        }

        private static string GenerateHashFrom(long[] numbers, HashIdOptions options)
        {
            if (numbers == null || numbers.Length == 0 || numbers.Any(n => n < 0))
                return string.Empty;

            long numbersHashInt = 0;
            for (var i = 0; i < numbers.Length; i++)
            {
                numbersHashInt += numbers[i] % (i + 100);
            }

            var builder = new StringBuilder();

            var alphabet = options.Alphabet.CopyPooled();
            var hashBuffer = new char [options.MaxNumberHashLength];

            var lottery = alphabet[numbersHashInt % options.alphabetLength];
            builder.Append(lottery);
            var shuffleBuffer = CreatePooledBuffer(options.Alphabet.Length, lottery, options: options);

            var startIndex = 1 + options.Salt.Length;
            var length = options.Alphabet.Length - startIndex;

            for (var i = 0; i < numbers.Length; i++)
            {
                var number = numbers[i];

                if (length > 0)
                {
                    Array.Copy(alphabet, 0, shuffleBuffer, startIndex, length);
                }

                ConsistentShuffle(alphabet, options.Alphabet.Length, shuffleBuffer, options.Alphabet.Length);
                var hashLength = BuildReversedHash(number, alphabet, hashBuffer, options: options);

                for (var j = hashLength - 1; j > -1; j--)
                {
                    builder.Append(hashBuffer[j]);
                }

                if (i + 1 < numbers.Length)
                {
                    number %= hashBuffer[hashLength - 1] + i;
                    var sepsIndex = number % options.Seps.Length;

                    builder.Append(options.Seps[sepsIndex]);
                }
            }

            if (builder.Length < options.MinHashLength)
            {
                var guardIndex = (numbersHashInt + builder[0]) % options.guards.Length;
                var guard = options.guards[guardIndex];

                builder.Insert(0, guard);

                if (builder.Length < options.MinHashLength)
                {
                    guardIndex = (numbersHashInt + builder[2]) % options.guards.Length;
                    guard = options.guards[guardIndex];

                    builder.Append(guard);
                }
            }

            var halfLength = options.Alphabet.Length / 2;

            while (builder.Length < options.MinHashLength)
            {
                Array.Copy(alphabet, shuffleBuffer, options.Alphabet.Length);
                ConsistentShuffle(alphabet, options.Alphabet.Length, shuffleBuffer, options.Alphabet.Length);
                builder.Insert(0, alphabet, halfLength, options.Alphabet.Length - halfLength);
                builder.Append(alphabet, 0, halfLength);

                var excess = builder.Length - options.MinHashLength;
                if (excess > 0)
                {
                    builder.Remove(0, excess / 2);
                    builder.Remove(options.MinHashLength, builder.Length - options.MinHashLength);
                }
            }

            var result = builder.ToString();
            return result;
        }

        private static int BuildReversedHash(long input, char[] alphabet, char[] hashBuffer, HashIdOptions options)
        {
            var length = 0;
            do
            {
                hashBuffer[length++] = alphabet[input % options.alphabetLength];
                input /= options.alphabetLength;
            } while (input > 0);

            return length;
        }

        private static string GenerateHashFrom(ulong[] numbers, HashIdOptions options)
        {
            if (numbers == null || numbers.Length == 0)
                return string.Empty;

            ulong numbersHashInt = 0;
            for (var i = 0; i < numbers.Length; i++)
            {
                numbersHashInt += numbers[i] % ((ulong)(i + 100));
            }

            var builder = new StringBuilder();

            var alphabet = options.Alphabet.CopyPooled();
            var hashBuffer = new char[options.MaxNumberHashLength];

            var lottery = alphabet[numbersHashInt % ((ulong)options.alphabetLength)];
            builder.Append(lottery);
            var shuffleBuffer = CreatePooledBuffer(options.Alphabet.Length, lottery, options: options);

            var startIndex = 1 + options.Salt.Length;
            var length = options.Alphabet.Length - startIndex;

            for (var i = 0ul; i < ((ulong)numbers.LongLength); i++)
            {
                var number = numbers[i];

                if (length > 0)
                {
                    Array.Copy(alphabet, 0, shuffleBuffer, startIndex, length);
                }

                ConsistentShuffle(alphabet, options.Alphabet.Length, shuffleBuffer, options.Alphabet.Length);
                var hashLength = BuildReversedHash(number, alphabet, hashBuffer, options: options);

                for (var j = hashLength - 1; j > -1; j--)
                {
                    builder.Append(hashBuffer[j]);
                }

                if (i + 1 < ((ulong)numbers.LongLength))
                {
                    number %= hashBuffer[hashLength - 1] + i;
                    var sepsIndex = number % ((ulong)options.Seps.Length);

                    builder.Append(options.Seps[sepsIndex]);
                }
            }

            if (builder.Length < options.MinHashLength)
            {
                var guardIndex = (numbersHashInt + builder[0]) % ((ulong)options.guards.Length);
                var guard = options.guards[guardIndex];

                builder.Insert(0, guard);

                if (builder.Length < options.MinHashLength)
                {
                    guardIndex = (numbersHashInt + builder[2]) % ((ulong)options.guards.Length);
                    guard = options.guards[guardIndex];

                    builder.Append(guard);
                }
            }

            var halfLength = options.Alphabet.Length / 2;

            while (builder.Length < options.MinHashLength)
            {
                Array.Copy(alphabet, shuffleBuffer, options.Alphabet.Length);
                ConsistentShuffle(alphabet, options.Alphabet.Length, shuffleBuffer, options.Alphabet.Length);
                builder.Insert(0, alphabet, halfLength, options.Alphabet.Length - halfLength);
                builder.Append(alphabet, 0, halfLength);

                var excess = builder.Length - options.MinHashLength;
                if (excess > 0)
                {
                    builder.Remove(0, excess / 2);
                    builder.Remove(options.MinHashLength, builder.Length - options.MinHashLength);
                }
            }

            var result = builder.ToString();
            return result;
        }

        private static int BuildReversedHash(ulong input, char[] alphabet, char[] hashBuffer, HashIdOptions options)
        {
            var length = 0;
            do
            {
                hashBuffer[length++] = alphabet[input % ((ulong)options.alphabetLength)];
                input /= ((ulong)options.alphabetLength);
            } while (input > 0);

            return length;
        }

        private static long Unhash(string input, char[] alphabet, HashIdOptions options)
        {
            long number = 0;

            for (var i = 0; i < input.Length; i++)
            {
                var pos = Array.IndexOf(alphabet, input[i]);
                number = number * options.alphabetLength + pos;
            }

            return number;
        }

        private static long[] GetNumbersFrom(string hash, HashIdOptions options)
        {
            if (string.IsNullOrWhiteSpace(hash))
                return Array.Empty<long>();

            var hashArray = hash.Split(options.guards, StringSplitOptions.RemoveEmptyEntries);
            if (hashArray.Length == 0)
                return Array.Empty<long>();

            var i = 0;
            if (hashArray.Length == 3 || hashArray.Length == 2)
            {
                i = 1;
            }

            var hashBreakdown = hashArray[i];
            var lottery = hashBreakdown[0];

            if (lottery == default(char))
                return Array.Empty<long>();

            hashBreakdown = hashBreakdown.Substring(1);

            hashArray = hashBreakdown.Split(options.Seps, StringSplitOptions.RemoveEmptyEntries);

            var result = new long[hashArray.Length];
            var alphabet = options.Alphabet.CopyPooled();
            var buffer = CreatePooledBuffer(options.Alphabet.Length, lottery, options: options);

            var startIndex = 1 + options.Salt.Length;
            var length = options.Alphabet.Length - startIndex;

            for (var j = 0; j < hashArray.Length; j++)
            {
                var subHash = hashArray[j];

                if (length > 0)
                {
                    Array.Copy(alphabet, 0, buffer, startIndex, length);
                }

                ConsistentShuffle(alphabet, options.Alphabet.Length, buffer, options.Alphabet.Length);
                result[j] = Unhash(subHash, alphabet, options: options);
            }

            if (result.EncodeHashId(options:options) == hash)
            {
                return result;
            }

            return Array.Empty<long>();
        }

        private static char[] CreatePooledBuffer(int alphabetLength, char lottery,
            HashIdOptions options)
        {
            var buffer = new char [alphabetLength];
            buffer[0] = lottery;
            Array.Copy(options.Salt, 0, buffer, 1, Math.Min(options.Salt.Length, alphabetLength - 1));
            return buffer;
        }

        internal static void ConsistentShuffle(char[] alphabet, int alphabetLength, char[] salt, int saltLength)
        {
            if (salt.Length == 0)
                return;

            int n;
            for (int i = alphabetLength - 1, v = 0, p = 0; i > 0; i--, v++)
            {
                v %= saltLength;
                p += (n = salt[v]);
                var j = (n + v + p) % i;
                // swap characters at positions i and j
                var temp = alphabet[j];
                alphabet[j] = alphabet[i];
                alphabet[i] = temp;
            }
        }

        
    }

    public class HashIdOptions
    {
        public const string DEFAULT_ALPHABET = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";
        public const string DEFAULT_SEPS = "cfhistuCFHISTU";
        public const int MIN_ALPHABET_LENGTH = 16;

        private const double SEP_DIV = 3.5;
        private const double GUARD_DIV = 12.0;

        private const int maxNumberHashLength = 12; // Length of long.MaxValue;

        public char[] Salt;
        public int MinHashLength;
        public char[] Alphabet;
        public char[] Seps;

        internal long alphabetLength;
        internal char[] guards;

        public int MaxNumberHashLength => maxNumberHashLength;

        /// <summary>
        /// Instantiates a new Hashids encoder/decoder.
        /// All parameters are optional and will use defaults unless otherwise specified.
        /// </summary>
        /// <param name="salt"></param>
        /// <param name="minHashLength"></param>
        /// <param name="alphabet"></param>
        /// <param name="seps"></param>
        public HashIdOptions(
            string salt = "",
            int minHashLength = 0,
            string alphabet = DEFAULT_ALPHABET,
            string seps = DEFAULT_SEPS)
        {
            if (salt is null)
                throw new ArgumentNullException(nameof(salt));
            if (string.IsNullOrWhiteSpace(alphabet))
                throw new ArgumentNullException(nameof(alphabet));
            if (minHashLength < 0)
                throw new ArgumentOutOfRangeException(nameof(minHashLength), "Value must be zero or greater.");
            if (string.IsNullOrWhiteSpace(seps))
                throw new ArgumentNullException(nameof(seps));

            Salt = salt.Trim().ToCharArray();
            Alphabet = alphabet.ToCharArray().Distinct().ToArray();
            Seps = seps.ToCharArray();
            MinHashLength = minHashLength;

            if (Alphabet.Length < MIN_ALPHABET_LENGTH)
                throw new ArgumentException($"Alphabet must contain at least {MIN_ALPHABET_LENGTH} unique characters.",
                    nameof(alphabet));

            SetupSeps();
            SetupGuards();

            alphabetLength = Alphabet.Length;
        }

        private void SetupSeps()
        {
            // seps should contain only characters present in alphabet; 
            Seps = Seps.Intersect(Alphabet).ToArray();

            // alphabet should not contain seps.
            Alphabet = Alphabet.Except(Seps).ToArray();

            HashIdExtensions.ConsistentShuffle(Seps, Seps.Length, Salt, Salt.Length);

            if (Seps.Length == 0 || ((float)Alphabet.Length / Seps.Length) > SEP_DIV)
            {
                var sepsLength = (int)Math.Ceiling((float)Alphabet.Length / SEP_DIV);

                if (sepsLength == 1)
                {
                    sepsLength = 2;
                }

                if (sepsLength > Seps.Length)
                {
                    var diff = sepsLength - Seps.Length;
                    Seps = Seps.Append(Alphabet, 0, diff);
                    Alphabet = Alphabet.SubArray(diff);
                }
                else
                {
                    Seps = Seps.SubArray(0, sepsLength);
                }
            }

            HashIdExtensions.ConsistentShuffle(Alphabet, Alphabet.Length, Salt, Salt.Length);
        }

        private void SetupGuards()
        {
            var guardCount = (int)Math.Ceiling(Alphabet.Length / GUARD_DIV);

            if (Alphabet.Length < 3)
            {
                guards = Seps.SubArray(0, guardCount);
                Seps = Seps.SubArray(guardCount);
            }

            else
            {
                guards = Alphabet.SubArray(0, guardCount);
                Alphabet = Alphabet.SubArray(guardCount);
            }
        }
    }

    internal static class ArrayExtensions
    {
        public static T[] SubArray<T>(this T[] array, int index)
        {
            return SubArray(array, index, array.Length - index);
        }

        public static T[] SubArray<T>(this T[] array, int index, int length)
        {
            var subarray = new T[length];
            Array.Copy(array, index, subarray, 0, length);
            return subarray;
        }

        public static T[] Append<T>(this T[] array, T[] appendArray, int index, int length)
        {
            var newArray = new T[array.Length + length - index];
            Array.Copy(array, 0, newArray, 0, array.Length);
            Array.Copy(appendArray, index, newArray, array.Length, length - index);
            return newArray;
        }

        public static T[] CopyPooled<T>(this T[] array)
        {
            var subarray = new T[array.Length];
            Array.Copy(array, 0, subarray, 0, array.Length);
            return subarray;
        }
    }
}