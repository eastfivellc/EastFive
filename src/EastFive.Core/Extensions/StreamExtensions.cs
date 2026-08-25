using EastFive;
using EastFive.Extensions;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace EastFive
{
    public static class StreamExtensions
    {
        public static async Task<byte []> ToBytesAsync(this Stream stream)
        {
            using (var memoryStream = await stream.ToCachedStreamAsync())
            {
                var bytes = memoryStream.ToArray();
                return bytes;
            }
        }

        public static async Task<MemoryStream> ToCachedStreamAsync(this Stream stream)
        {
            var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            return memoryStream;
        }

        public static byte [] ToBytes(this Stream stream)
        {
            using (var memoryStream = stream.ToCachedStream())
            {
                var bytes = memoryStream.ToArray();
                return bytes;
            }
        }

        public static MemoryStream ToCachedStream(this Stream stream)
        {
            var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            memoryStream.Position = 0;
            return memoryStream;
        }

        /// <summary>
        /// This method is ideally used to create a unique string.  WARNING: Using this method to protect sensitive information would be a security vulnerability due to it using a weak hashing function.
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static string Md5Checksum(this Stream stream)
        {
            #pragma warning disable SCS0006 // Weak hashing function
            using (var algorithm = MD5.Create())
            {
                var hash = algorithm.ComputeHash(stream);
                var result = hash.Select(hex => hex.ToString("X2"))
                    .Join("")
                    .ToUpper();
                return result;
            }
            #pragma warning restore SCS0006 // Weak hashing function
        }


        /// <summary>
        /// This method is ideally used to create a unique string.  WARNING: Using this method to protect sensitive information would be a security vulnerability due to it using a weak hashing function.
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static string Md5Checksum(this byte[] bytes)
        {
            #pragma warning disable SCS0006 // Weak hashing function
            using (var algorithm = MD5.Create())
            {
                var hash = algorithm.ComputeHash(bytes);
                var result = hash.Select(hex => hex.ToString("X2"))
                    .Join("")
                    .ToUpper();
                return result;
            }
            #pragma warning restore SCS0006 // Weak hashing function
        }

        public static Task<string> ReadAsStringAsync(this Stream stream, 
            Encoding encoding = default(Encoding))
        {
            if (encoding.IsDefault())
                encoding = Encoding.UTF8;
            using (var reader = new StreamReader(stream, encoding))
            {
                var value = reader.ReadToEndAsync();
                return value;
            }
        }
    }
}
