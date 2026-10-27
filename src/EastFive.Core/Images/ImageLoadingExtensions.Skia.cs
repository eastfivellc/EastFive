using System;
using System.IO;
using System.Threading.Tasks;

using SkiaSharp;

using EastFive.Extensions;

namespace EastFive.Images
{
    public static partial class ImageLoadingExtensions
    {
        /// <summary>
        /// Decodes <paramref name="mediaContents"/>. Not a decodable image takes
        /// <paramref name="onFailure"/>, with the stream rewound when it can seek. The bitmap is
        /// NOT oriented: apply <see cref="ImageExifExtensions.FixOrientation(SKBitmap, SKEncodedOrigin)"/>
        /// with the origin read from the encoded bytes.
        /// </summary>
        public static async Task<TResult> TryReadImageAsync<TResult>(this Stream mediaContents,
            Func<SKBitmap, SKEncodedImageFormat, TResult> onRead,
            Func<TResult> onFailure)
        {
            var bytes = await mediaContents.ToBytesAsync();
            if (bytes.TryReadImage(out var image, out var format))
                return onRead(image, format);

            if (mediaContents.CanSeek)
                mediaContents.Position = 0;
            return onFailure();
        }

        public static async Task<TResult> TryReadImageAsync<TResult>(this byte[] mediaContents,
            Func<SKBitmap, SKEncodedImageFormat, TResult> onRead,
            Func<TResult> onFailure)
        {
            using (var ms = new MemoryStream(mediaContents))
            {
                return await ms.TryReadImageAsync(onRead, onFailure);
            }
        }

        public static async Task<(bool success, SKBitmap image, SKEncodedImageFormat format)> TryReadImageAsync(this Stream mediaContents)
        {
            var bytes = await mediaContents.ToBytesAsync();
            return bytes.TryReadImage(out var image, out var format)
                ? (true, image, format)
                : (false, default, default);
        }

        public static bool TryReadImage(this byte[] mediaContents, out SKBitmap image, out SKEncodedImageFormat format)
        {
            using (var data = SKData.CreateCopy(mediaContents))
            using (var codec = SKCodec.Create(data))
            {
                var decoded = codec is null ? null : SKBitmap.Decode(codec);
                if (decoded is null)
                {
                    image = default;
                    format = default;
                    return false;
                }
                image = decoded;
                format = codec!.EncodedFormat;
                return true;
            }
        }

        /// <summary>The image's size and pixel format, read from its header without decoding the pixels.</summary>
        public static Task<SKImageInfo> TryReadImageMetadata(this byte[] mediaContents)
        {
            using (var data = SKData.CreateCopy(mediaContents))
            using (var codec = SKCodec.Create(data))
            {
                if (codec is null)
                    throw new ArgumentException("The content is not a decodable image.", nameof(mediaContents));
                return Task.FromResult(codec.Info);
            }
        }

        public static async Task SaveWithQualityAsync(this SKBitmap image, Stream outputStream,
            SKEncodedImageFormat imageCodec, long encoderQuality = 80L)
        {
            var bytes = image.EncodeBytes(imageCodec, encoderQuality);
            await outputStream.WriteAsync(bytes, 0, bytes.Length);
        }

        public static async Task<SKEncodedImageFormat> SaveAsync(this SKBitmap image, Stream outputStream,
            string encodingMimeType, long encoderQuality = 80L)
        {
            var format = encodingMimeType.ParseImageEncoder();
            await image.SaveWithQualityAsync(outputStream, format, encoderQuality);
            return format;
        }

        public static Task<(byte[], SKEncodedImageFormat)> GetBytesAsync(this SKBitmap image,
            string encodingMimeType = "image/jpeg", long encoderQuality = 80L)
        {
            return Task.FromResult(image.Save(encodingMimeType, encoderQuality));
        }

        public static (byte[], SKEncodedImageFormat) Save(this SKBitmap image,
            string encodingMimeType = "image/jpeg", long encoderQuality = 80L)
        {
            var format = encodingMimeType.ParseImageEncoder();
            return (image.EncodeBytes(format, encoderQuality), format);
        }

        /// <summary>
        /// The format to ENCODE a requested mime type as. Skia encodes PNG, JPEG and WEBP only
        /// (it decodes more), so every other request, a missing one included, is answered with
        /// PNG: lossless, and readable everywhere. Read the result's
        /// <see cref="ToMimeType(SKEncodedImageFormat)"/> for what was actually written rather
        /// than assuming it was the type asked for.
        /// </summary>
        public static SKEncodedImageFormat ParseImageEncoder(this string encodingMimeType)
        {
            return encodingMimeType?.Split(';')[0].Trim().ToLowerInvariant() switch
            {
                "image/jpeg" or "image/jpg" or "image/pjpeg" => SKEncodedImageFormat.Jpeg,
                "image/webp" => SKEncodedImageFormat.Webp,
                _ => SKEncodedImageFormat.Png,
            };
        }

        /// <summary>The mime type a format is served as.</summary>
        public static string ToMimeType(this SKEncodedImageFormat format)
        {
            return format switch
            {
                SKEncodedImageFormat.Png => "image/png",
                SKEncodedImageFormat.Jpeg => "image/jpeg",
                SKEncodedImageFormat.Webp => "image/webp",
                SKEncodedImageFormat.Gif => "image/gif",
                SKEncodedImageFormat.Bmp => "image/bmp",
                SKEncodedImageFormat.Ico => "image/x-icon",
                SKEncodedImageFormat.Wbmp => "image/vnd.wap.wbmp",
                SKEncodedImageFormat.Heif => "image/heif",
                SKEncodedImageFormat.Avif => "image/avif",
                _ => "application/octet-stream",
            };
        }

        public static bool TryParseImage(this string imageDataEncoding, out SKBitmap image)
            => imageDataEncoding.TryParseImage(out image, out SKEncodedImageFormat discard);

        /// <summary>Decodes a data URI or bare base64. Data that is not a decodable image is false, not a throw.</summary>
        public static bool TryParseImage(this string imageDataEncoding, out SKBitmap image, out SKEncodedImageFormat imageFormat)
        {
            if (!imageDataEncoding.TryParseImage(
                out byte[] data, out string contentType))
            {
                image = default;
                imageFormat = default;
                return false;
            }

            return data.TryReadImage(out image, out imageFormat);
        }

        public static string Base64Encode(this SKBitmap image,
            string encodingMimeType = "image/jpeg", long encoderQuality = 80L)
        {
            var (mediaContents, format) = image.Save(encodingMimeType, encoderQuality);
            return $"data:{format.ToMimeType()};base64,{mediaContents.ToBase64String()}";
        }

        /// <summary>
        /// The type <see cref="Base64Encode(SKBitmap, string, long)"/> and
        /// <see cref="Save(SKBitmap, string, long)"/> write by default. A decoded bitmap keeps no
        /// record of the format it came from; use the format returned by the decode for that.
        /// </summary>
        public static string GetMimeType(this SKBitmap image)
        {
            return "image/jpeg";
        }

        private static byte[] EncodeBytes(this SKBitmap image, SKEncodedImageFormat format, long encoderQuality)
        {
            using (var data = image.Encode(format, (int)Math.Clamp(encoderQuality, 0L, 100L)))
            {
                if (data is null)
                    throw new InvalidOperationException($"Skia could not encode the image as {format}.");
                return data.ToArray();
            }
        }
    }
}
