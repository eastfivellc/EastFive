using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using SixLabors.ImageSharp;

using EastFive.Extensions;
using EastFive.Linq;
using SixLabors.ImageSharp.Formats;
using EastFive.Text;
using SixLabors.ImageSharp.Metadata;

namespace EastFive.Images
{
    public static partial class ImageLoadingExtensions
    {
        public static async Task<TResult> TryReadImageAsync<TResult>(this Stream mediaContents,
            Func<Image, IImageFormat , TResult> onRead,
            Func<TResult> onFailure)
        {
            try
            {
                var image = await Image.LoadAsync(mediaContents);
                var format = image.Metadata.DecodedImageFormat;
                return onRead(image, format);
            } catch(ArgumentException)
            {
                mediaContents.Position = 0;
                return onFailure();
            }
        }

        public static async Task<TResult> TryReadImageAsync<TResult>(this byte[] mediaContents,
            Func<Image, IImageFormat, TResult> onRead,
            Func<TResult> onFailure)
        {
            using(var ms = new MemoryStream(mediaContents))
            {
                return await ms.TryReadImageAsync(onRead, onFailure);
            }
        }

        public static async Task<(bool success, Image image, IImageFormat format)> TryReadImageAsync(this Stream mediaContents)
        {
            try
            {
                var image = await Image.LoadAsync(mediaContents);
                var format = image.Metadata.DecodedImageFormat;
                return (true, image, format);
            }
            catch (UnknownImageFormatException)
            {
                return (false, default, default);
            }
        }

        public static bool TryReadImage(this byte [] mediaContents, out Image image, out IImageFormat format)
        {
            try
            {
                image = Image.Load(mediaContents);
                format = image.Metadata.DecodedImageFormat;
                return true;
            }catch (UnknownImageFormatException)
            {
                image = default;
                format = default;
                return false;
            }
        }

        public static async Task<ImageInfo> TryReadImageMetadata(this byte[] mediaContents)
        {
            using (var stream = new MemoryStream(mediaContents))
            {
                return await Image.IdentifyAsync(stream);
            }
        }

        public static Task SaveWithQualityAsync(this Image image, Stream outputStream,
            IImageFormat imageCodec, long encoderQuality = 80L)
        {
            return image.SaveAsync(outputStream, imageCodec);
        }

        public static async Task<IImageFormat> SaveAsync(this Image image, Stream outputStream,
            string encodingMimeType, long encoderQuality = 80L)
        {
            var format = encodingMimeType.ParseImageEncoder();
            await image.SaveAsync(outputStream, format);
            return format;
        }

        public static async Task<(byte [], IImageFormat)> GetBytesAsync(this Image image,
            string encodingMimeType = "image/jpeg", long encoderQuality = 80L)
        {
            using (var stream = new MemoryStream())
            {
                var format = encodingMimeType.ParseImageEncoder();
                await image.SaveAsync(stream, format);
                return (stream.ToArray(), format);
            }
        }

        public static (byte[], IImageFormat) Save(this Image image,
            string encodingMimeType = "image/jpeg", long encoderQuality = 80L)
        {
            using (var stream = new MemoryStream())
            {
                var format = encodingMimeType.ParseImageEncoder();
                image.Save(stream, format);
                return (stream.ToArray(), format);
            }
        }

        public static IImageFormat ParseImageEncoder(this string encodingMimeType)
        {
            return SixLabors.ImageSharp.Configuration.Default.ImageFormats
                .Where(format => format.MimeTypes.Any(mt => mt.Equals(encodingMimeType, StringComparison.OrdinalIgnoreCase)))
                .Max(
                    format =>
                    {
                        if (format.DefaultMimeType.Equals(encodingMimeType, StringComparison.Ordinal))
                            return 4;

                        if (format.DefaultMimeType.Equals(encodingMimeType, StringComparison.OrdinalIgnoreCase))
                            return 3;

                        if (format.MimeTypes.Any(mt => mt.Equals(encodingMimeType, StringComparison.Ordinal)))
                            return 2;

                        if (format.MimeTypes.Any(mt => mt.Equals(encodingMimeType, StringComparison.OrdinalIgnoreCase)))
                            return 1;

                        return 0;
                    },
                    (format) =>
                    {
                        return format;
                    },
                    () =>
                    {
                        return SixLabors.ImageSharp.Configuration.Default.ImageFormats
                            .Min(
                                format =>
                                {
                                    return format.MimeTypes
                                        .Append(format.DefaultMimeType)
                                        .Min(mt => mt.Levenshtein(encodingMimeType));
                                },
                                (format) =>
                                {
                                    return format;
                                },
                                () =>
                                {
                                    throw new Exception(
                                        "Default ImageSharp configuration does not have any formats loaded.");
                                });
                    });
        }

        public static bool TryParseImage(this string imageDataEncoding, out Image image)
            => imageDataEncoding.TryParseImage(out image, out IImageFormat discard);

        public static bool TryParseImage(this string imageDataEncoding, out Image image, out IImageFormat imageFormat)
        {
            if (!imageDataEncoding.TryParseImage(
                out byte[] data, out string contentType))
            {
                image = default;
                imageFormat = default;
                return false;
            }

            using (var stream = new MemoryStream(data))
            {
                image = Image.Load(data);
                imageFormat = image.Metadata.DecodedImageFormat;
                return true;
            }
        }

        public static string Base64Encode(this Image image,
            string encodingMimeType = "image/jpeg", long encoderQuality = 80L)
        {
            var (mediaContents, imageCodecInfo) = image.Save(encodingMimeType: encodingMimeType);
            var contentType = imageCodecInfo.DefaultMimeType;
            return $"data:{contentType};base64,{mediaContents.ToBase64String()}";
        }

        public static string GetMimeType(this Image image)
        {
            return "image/jpeg";
        }
    }
}
