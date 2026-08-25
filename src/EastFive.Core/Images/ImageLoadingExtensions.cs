using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using EastFive.Extensions;
using EastFive.Linq;

namespace EastFive.Images
{
    public static partial class ImageLoadingExtensions
    {
        [SupportedOSPlatform("windows6.1")]
        public static bool TryReadImage(this Stream mediaContents, out Image image)
        {
            if (!OperatingSystem.IsWindows())
                throw new NotSupportedException("OS not supported");

            try
            {
                image = Bitmap.FromStream(mediaContents);
                return true;
            } catch(ArgumentException)
            {
                mediaContents.Position = 0;
                image = default;
                return false;
            }
        }

        [SupportedOSPlatform("windows6.1")]
        public static bool TryReadImage(this byte [] mediaContents, out Image image)
        {
            using (var stream = new MemoryStream(mediaContents))
                return stream.TryReadImage(out image);
        }

        [SupportedOSPlatform("windows6.1")]
        public static void Save(this Image image, Stream outputStream,
            ImageCodecInfo imageCodec, long encoderQuality = 80L)
        {
            if (!OperatingSystem.IsWindows())
                throw new NotSupportedException("OS not supported");

            var encoderParameters = new EncoderParameters(1);
            encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, encoderQuality);

            image.Save(outputStream, imageCodec, encoderParameters);
        }

        [SupportedOSPlatform("windows6.1")]
        public static void Save(this Image image, Stream outputStream,
            out ImageCodecInfo imageCodecUsed,
            string encodingMimeType = "image/jpeg", long encoderQuality = 80L)
        {
            imageCodecUsed = ParseImageCodecInfo(encodingMimeType);
            image.Save(outputStream, imageCodecUsed, encoderQuality);
        }

        [SupportedOSPlatform("windows6.1")]
        public static byte [] Save(this Image image,
            out ImageCodecInfo imageCodecUsed,
            string encodingMimeType = "image/jpeg", long encoderQuality = 80L)
        {
            using(var stream = new MemoryStream())
            {
                image.Save(stream, out imageCodecUsed,
                    encodingMimeType: encodingMimeType,
                    encoderQuality: encoderQuality);
                return stream.ToArray();
            }
        }

        public static ImageCodecInfo ParseImageCodecInfo(this string mimeType)
        {
            if (!OperatingSystem.IsWindows())
                throw new NotSupportedException("OS not supported");

            #pragma warning disable CA1416
            return ImageCodecInfo
                .GetImageEncoders()
                .First(
                    (encoder, next) =>
                    {
                        if (encoder.MimeType.Equals(mimeType, StringComparison.OrdinalIgnoreCase))
                            return encoder;
                        return next();
                    },
                    () => ImageCodecInfo.GetImageEncoders().First());
            #pragma warning restore CA1416

        }

        [SupportedOSPlatform("windows6.1")]
        public static bool TryParseImage(this string imageDataEncoding, out Image image)
        {
            if (!OperatingSystem.IsWindows())
                throw new NotSupportedException("OS not supported");

            if (!imageDataEncoding.TryParseImage(
                out byte [] data, out string contentType))
            {
                image = default;
                return false;
            }

            using (var stream = new MemoryStream(data))
            {
                image = new Bitmap(stream);
                return true;
            }
        }

        public static bool TryParseImage(this string imageDataEncoding,
            out byte [] contents, out string contentType)
        {
            if (!imageDataEncoding.TryMatchRegex(
                "data:(?<contentType>[^;]+);(?<encoding>[^,]+),(?<data>[\\S\\s]+)",
                (contentType, encoding, data) => Tuple.Create(contentType, encoding, data),
                out Tuple<string, string, string> components))
            {
                contents = default;
                contentType = default;
                return false;
            }

            contentType = components.Item1;
            var encoding = components.Item2;
            var dataEncoded = components.Item3;
            if (encoding.Equals("base64", StringComparison.OrdinalIgnoreCase))
            {
                contents = dataEncoded.FromBase64String();
                return true;
            }

            if (encoding.Equals("base58", StringComparison.OrdinalIgnoreCase))
            {
                contents = dataEncoded.Base58Decode();
                return true;
            }

            contents = default;
            return false;
        }

        [SupportedOSPlatform("windows6.1")]
        public static string Base64Encode(this Image image,
            string encodingMimeType = "image/jpeg", long encoderQuality = 80L)
        {
            if (!OperatingSystem.IsWindows())
                return default;

            var mediaContents = image.Save(out ImageCodecInfo imageCodecInfo,
                encodingMimeType:encodingMimeType, encoderQuality:encoderQuality);
            var contentType = imageCodecInfo.MimeType;
            return $"data:{contentType};base64,{mediaContents.ToBase64String()}";
        }

        [SupportedOSPlatform("windows6.1")]
        public static string GetMimeType(this Image image)
        {
            if (!OperatingSystem.IsWindows())
                throw new NotSupportedException("OS not supported");

            return image.RawFormat.GetMimeType();
        }

        public static string GetMimeType(this ImageFormat imageFormat)
        {
            if (!OperatingSystem.IsWindows())
                throw new NotSupportedException("OS not supported");

            #pragma warning disable CA1416
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            return codecs.First(codec => codec.FormatID == imageFormat.Guid).MimeType;
            #pragma warning restore CA1416
        }
    }
}
