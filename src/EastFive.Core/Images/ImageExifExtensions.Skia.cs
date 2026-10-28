using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

using SkiaSharp;

namespace EastFive.Images
{
    /// <summary>
    /// EXIF for the Skia surface. A decoded <see cref="SKBitmap"/> carries NO metadata, so every
    /// reader here takes the ENCODED bytes: orientation through <see cref="SKCodec.EncodedOrigin"/>,
    /// dates and GPS through MetadataExtractor. Nothing writes EXIF: <see cref="FixOrientation"/>
    /// applies the orientation to the pixels, after which no tag is needed.
    /// </summary>
    public static partial class ImageExifExtensions
    {
        /// <summary>The EXIF orientation of the encoded image; TopLeft (upright) when it has none or is not an image.</summary>
        public static SKEncodedOrigin ExifGetRotateFlip(this byte[] encoded)
        {
            using (var data = SKData.CreateCopy(encoded))
            using (var codec = SKCodec.Create(data))
                return codec?.EncodedOrigin ?? SKEncodedOrigin.TopLeft;
        }

        public static SKEncodedOrigin ExifGetRotateFlip(this Stream encoded)
        {
            using (var memory = new MemoryStream())
            {
                encoded.CopyTo(memory);
                return memory.ToArray().ExifGetRotateFlip();
            }
        }

        /// <summary>
        /// Rotates and flips <paramref name="image"/>'s PIXELS so it displays upright for
        /// <paramref name="origin"/>, in place, and returns it. <c>SKBitmap.Decode</c> does NOT
        /// do this, so call it with the origin read from the same bytes
        /// (<see cref="ExifGetRotateFlip(byte[])"/>).
        /// </summary>
        public static SKBitmap FixOrientation(this SKBitmap image, SKEncodedOrigin origin)
        {
            if (!origin.TryOrientationToFlipRotate(out var degrees, out var flipHorizontal)
                    || (degrees == 0 && !flipHorizontal))
                return image;

            var quarterTurn = degrees is 90 or 270;
            var width = quarterTurn ? image.Height : image.Width;
            var height = quarterTurn ? image.Width : image.Height;
            var oriented = new SKBitmap(new SKImageInfo(width, height, image.ColorType, image.AlphaType));
            using (var canvas = new SKCanvas(oriented))
            {
                if (flipHorizontal)
                {
                    canvas.Translate(width, 0);
                    canvas.Scale(-1, 1);
                }
                canvas.Translate(width / 2f, height / 2f);
                canvas.RotateDegrees(degrees);
                canvas.Translate(-image.Width / 2f, -image.Height / 2f);
                // Nearest: a quarter turn maps pixel centres onto pixel centres, so this copies exactly.
                canvas.DrawBitmap(image, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest), null);
            }
            return image.ReplaceWith(oriented);
        }

        /// <summary>The origin composed with a half turn, as the <c>Orientation</c> version always answered.</summary>
        public static SKEncodedOrigin Invert(this SKEncodedOrigin xform)
        {
            return xform switch
            {
                SKEncodedOrigin.TopLeft => SKEncodedOrigin.BottomRight,
                SKEncodedOrigin.BottomRight => SKEncodedOrigin.TopLeft,
                SKEncodedOrigin.LeftBottom => SKEncodedOrigin.RightTop,
                SKEncodedOrigin.RightTop => SKEncodedOrigin.LeftBottom,
                SKEncodedOrigin.TopRight => SKEncodedOrigin.BottomLeft,
                SKEncodedOrigin.BottomLeft => SKEncodedOrigin.TopRight,
                SKEncodedOrigin.RightBottom => SKEncodedOrigin.LeftTop,
                SKEncodedOrigin.LeftTop => SKEncodedOrigin.RightBottom,
                _ => throw new ArgumentException($"'{xform}' is not a recognized transform"),
            };
        }

        /// <summary>
        /// What displays <paramref name="origin"/> upright: a CLOCKWISE rotation of
        /// <paramref name="rotateDegrees"/>, then a horizontal flip when
        /// <paramref name="flipHorizontal"/>. Orientation 6 (RightTop), a phone held upright, is
        /// 90° clockwise.
        /// </summary>
        public static bool TryOrientationToFlipRotate(this SKEncodedOrigin origin,
            out int rotateDegrees, out bool flipHorizontal)
        {
            (bool known, int degrees, bool flip) answer = origin switch
            {
                SKEncodedOrigin.TopLeft => (true, 0, false),
                SKEncodedOrigin.TopRight => (true, 0, true),
                SKEncodedOrigin.BottomRight => (true, 180, false),
                SKEncodedOrigin.BottomLeft => (true, 180, true),
                SKEncodedOrigin.LeftTop => (true, 90, true),
                SKEncodedOrigin.RightTop => (true, 90, false),
                SKEncodedOrigin.RightBottom => (true, 270, true),
                SKEncodedOrigin.LeftBottom => (true, 270, false),
                _ => (false, 0, false),
            };
            rotateDegrees = answer.degrees;
            flipHorizontal = answer.flip;
            return answer.known;
        }

        /// <summary>When the picture was taken: DateTime, else DateTimeOriginal, else DateTimeDigitized; null when it has none.</summary>
        public static DateTime? ExifTaken(this byte[] encoded)
        {
            var directories = ReadMetadata(encoded);
            return new (Type directory, int tag)[]
                {
                    (typeof(ExifIfd0Directory), ExifDirectoryBase.TagDateTime),
                    (typeof(ExifSubIfdDirectory), ExifDirectoryBase.TagDateTimeOriginal),
                    (typeof(ExifSubIfdDirectory), ExifDirectoryBase.TagDateTimeDigitized),
                }
                .SelectMany(candidate => directories
                    .Where(directory => directory.GetType() == candidate.directory)
                    .Select(directory => directory.GetString(candidate.tag)))
                .Select(value => DateTime.TryParseExact(value, "yyyy:MM:dd HH:mm:ss",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var taken)
                    ? taken
                    : default(DateTime?))
                .FirstOrDefault(taken => taken.HasValue);
        }

        /// <summary>Latitude in signed decimal degrees (south negative), else the destination latitude; null when it has neither.</summary>
        public static double? ExifLat(this byte[] encoded) =>
            Coordinate(ReadMetadata(encoded),
                (GpsDirectory.TagLatitude, GpsDirectory.TagLatitudeRef),
                (GpsDirectory.TagDestLatitude, GpsDirectory.TagDestLatitudeRef));

        /// <summary>Longitude in signed decimal degrees (west negative), else the destination longitude; null when it has neither.</summary>
        public static double? ExifLon(this byte[] encoded) =>
            Coordinate(ReadMetadata(encoded),
                (GpsDirectory.TagLongitude, GpsDirectory.TagLongitudeRef),
                (GpsDirectory.TagDestLongitude, GpsDirectory.TagDestLongitudeRef));

        private static IReadOnlyList<MetadataExtractor.Directory> ReadMetadata(byte[] encoded)
        {
            try
            {
                using (var stream = new MemoryStream(encoded))
                    return ImageMetadataReader.ReadMetadata(stream);
            }
            catch (ImageProcessingException)
            {
                // Not an image format MetadataExtractor knows: no EXIF to read.
                return Array.Empty<MetadataExtractor.Directory>();
            }
        }

        private static double? Coordinate(IReadOnlyList<MetadataExtractor.Directory> directories,
            params (int coordinate, int reference)[] tags)
        {
            return directories
                .OfType<GpsDirectory>()
                .SelectMany(gps => tags.Select(tag => ParseCoordinate(gps, tag.coordinate, tag.reference)))
                .FirstOrDefault(value => value.HasValue);
        }

        private static double? ParseCoordinate(GpsDirectory gps, int coordinateTag, int referenceTag)
        {
            var parts = gps.GetRationalArray(coordinateTag);
            var reference = gps.GetString(referenceTag);
            if (parts is null || parts.Length < 3 || reference is null)
                return default;

            var degrees = parts[0].ToDouble() + parts[1].ToDouble() / 60.0 + parts[2].ToDouble() / 3600.0;
            return IsWestOrSouth(reference) ? -degrees : degrees;

            static bool IsWestOrSouth(string valueRef)
            {
                if (valueRef.Contains('w', StringComparison.OrdinalIgnoreCase))
                    return true;
                if (valueRef.Contains('s', StringComparison.OrdinalIgnoreCase))
                    if (!valueRef.Contains('e', StringComparison.OrdinalIgnoreCase))
                        return true;
                return false;
            }
        }
    }
}
