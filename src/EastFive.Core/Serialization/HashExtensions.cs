using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Reflection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace EastFive.Serialization
{
    public static class HashExtensions
    {
        public static byte[] SHA256Hash(this byte[] bytes, SHA256 sha256 = default(SHA256))
        {
            byte[] getHash(SHA256 algorithm) => algorithm.ComputeHash(bytes);

            if (default(SHA256) != sha256)
                return getHash(sha256);

            using (var algorithm = SHA256.Create())
                return getHash(algorithm);
        }

        public static Guid MD5HashGuid(this byte[] bytes, MD5 md5 = default(MD5))
        {
            if (bytes.IsDefaultNullOrEmpty())
                return default(Guid);

            #pragma warning disable SCS0006 // Weak hashing function
            Guid getHashGuid(MD5 algorithm) => new Guid(algorithm.ComputeHash(bytes));

            if (default(MD5) != md5)
                return getHashGuid(md5);

            using (var algorithm = MD5.Create())
                return getHashGuid(algorithm);
            #pragma warning restore SCS0006 // Weak hashing function
        }

        public static Guid MD5HashGuid(this string concatination, MD5 md5 = default(MD5))
        {
            var bytes = concatination.HasBlackSpace() ?
                Encoding.UTF8.GetBytes(concatination)
                :
                new byte[] { };

            return bytes.MD5HashGuid(md5);
        }

        public static Guid MD5HashGuid(this Stream stream, MD5 md5 = default(MD5))
        {
            #pragma warning disable SCS0006 // Weak hashing function
            Guid getHashGuid(MD5 algorithm) => new Guid(algorithm.ComputeHash(stream));

            if (default(MD5) != md5)
                return getHashGuid(md5);

            using (var algorithm = MD5.Create())
                return getHashGuid(algorithm);
            #pragma warning restore SCS0006 // Weak hashing function
        }

        /// <summary>
        /// This method is ideally used to create a unique string.  WARNING: Using this method to protect sensitive information would be a security vulnerability due to it using a weak hashing function.
        /// </summary>
        /// <param name="concatination"></param>
        /// <param name="md5"></param>
        /// <returns></returns>
        public static string MD5HashString(this string concatination, MD5 md5 = default(MD5))
        {
            #pragma warning disable SCS0006 // Weak hashing function
            string getHashString(MD5 algorithm) => Convert.ToBase64String(
                algorithm.ComputeHash(
                    Encoding.UTF8.GetBytes(concatination)));

            if (default(MD5) != md5)
                return getHashString(md5);

            using (var algorithm = MD5.Create())
                return getHashString(algorithm);
            #pragma warning restore SCS0006 // Weak hashing function
        }

        /// <summary>
        /// This method is ideally used to create a unique string.  WARNING: Using this method to protect sensitive information would be a security vulnerability due to it using a weak hashing function.
        /// </summary>
        /// <param name="concatination"></param>
        /// <param name="md5"></param>
        /// <returns></returns>
        public static string MD5HashHex(this string concatination, MD5 md5 = default(MD5))
        {
            #pragma warning disable SCS0006 // Weak hashing function
            string getHashHex(MD5 algorithm) => algorithm
                .ComputeHash(Encoding.UTF8.GetBytes(concatination))
                .Select(b => b.ToString("X2"))
                .Join("");

            if (default(MD5) != md5)
                return getHashHex(md5);

            using (var algorithm = MD5.Create())
                return getHashHex(algorithm);
            #pragma warning restore SCS0006 // Weak hashing function
        }

        public static byte[] SHAHash(this byte[] bytes, SHA256 sha256 = default(SHA256))
        {
            return bytes.SHA256Hash(sha256);
        }

        public static byte[] SHAHash(this string stringToHash,
            System.Text.Encoding encoding = default(System.Text.Encoding),
            SHA256 sha256 = default(SHA256))
        {
            if (default(System.Text.Encoding) == encoding)
                encoding = System.Text.Encoding.UTF8;
            var bytes = stringToHash.HasBlackSpace() ?
                encoding.GetBytes(stringToHash)
                :
                new byte[] { };
            return bytes.SHA256Hash(sha256);
        }
    }
}
