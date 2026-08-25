using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Processing;

using EastFive;
using EastFive.Extensions;
using EastFive.Linq;


namespace EastFive.Images
{
    public static partial class ImageExifExtensions
    {
        //public static void FixOrientation(this Image image)
        //{
        //    var orientation = image.ExifGetRotateFlip();
        //    if (orientation == Orientation.rotated0)
        //        return;

        //    orientation.TryOrientationToFlipRotate(
        //        out RotateMode rotateMode, out FlipMode flipMode);
        //    image.Mutate(ctx => ctx.RotateFlip(rotateMode, flipMode));
        //    image.ExifSetRotateFlip(Orientation.rotated0);
        //    //return image;
        //}

        public static Func<IImageProcessingContext, IImageProcessingContext> FixOrientation(this Image image)
        {
            var orientation = image.ExifGetRotateFlip();
            if (orientation == Orientation.rotated0)
                return ctx => ctx;

            orientation.TryOrientationToFlipRotate(
                out RotateMode rotateMode, out FlipMode flipMode);
            image.ExifSetRotateFlip(Orientation.rotated0);
            return ctx => ctx.RotateFlip(rotateMode, flipMode);
        }

        public static Orientation Invert(this Orientation xform)
        {
            // 0
            if (xform == Orientation.rotated0)
                return Orientation.rotated180;
            // 1
            if (xform == Orientation.rotated90)
                return Orientation.rotated270;
            // 2
            if (xform == Orientation.rotated180)
                return Orientation.rotated0;
            // 3
            if (xform == Orientation.rotated270)
                return Orientation.rotated90;
            // 4
            if (xform == Orientation.rotated0Mirrored)
                return Orientation.rotated180Mirrored;
            // 5
            if (xform == Orientation.rotated90Mirrored)
                return Orientation.rotated270Mirrored;
            // 6
            if (xform == Orientation.rotated180Mirrored)
                return Orientation.rotated0Mirrored;
            // 7
            if (xform == Orientation.rotated270Mirrored)
                return Orientation.rotated90Mirrored;

            throw new ArgumentException($"'{xform}' is not a recognized transform");
        }

        public static Orientation ExifGetRotateFlip(this Image image)
        {
            if (!image.Metadata.ExifProfile.Values.Contains(
                    v => v.Tag == ExifTag.Orientation))
                return Orientation.rotated0;

            var orientation = image.Metadata.ExifProfile.Values
                .Where(item => item.Tag == ExifTag.Orientation)
                .First();

            var orientationEnum = (Orientation)orientation.GetValue();
            return orientationEnum;
        }

        public static Image ExifSetRotateFlip(this Image image, Orientation orientation)
        {
            return image.Metadata.ExifProfile.Values
                .Where(item => item.Tag == ExifTag.Orientation)
                .First(
                    (propertyItem, next) =>
                    {
                        propertyItem.TrySetValue(
                                (ushort)orientation);
                        return image;
                    },
                    () =>
                    {
                        image.Metadata.ExifProfile
                            .SetValue(ExifTag.Orientation,
                                (ushort)orientation);
                        return image;
                    });
        }

        public static bool TryOrientationToFlipRotate(this Orientation orientation,
            out RotateMode rotateMode, out FlipMode flipMode)
        {
            if(orientation == Orientation.rotated0)
            {
                rotateMode = RotateMode.None;
                flipMode = FlipMode.None;
                return true;
            }
            if (orientation == Orientation.rotated0Mirrored)
            {
                rotateMode = RotateMode.None;
                flipMode = FlipMode.Horizontal;
                return true;
            }
            if (orientation == Orientation.rotated90)
            {
                rotateMode = RotateMode.Rotate90;
                flipMode = FlipMode.None;
                return true;
            }
            if (orientation == Orientation.rotated90Mirrored)
            {
                rotateMode = RotateMode.Rotate90;
                flipMode = FlipMode.Horizontal;
                return true;
            }
            if (orientation == Orientation.rotated180)
            {
                rotateMode = RotateMode.Rotate180;
                flipMode = FlipMode.None;
                return true;
            }
            if (orientation == Orientation.rotated180Mirrored)
            {
                rotateMode = RotateMode.Rotate180;
                flipMode = FlipMode.Horizontal;
                return true;
            }
            if (orientation == Orientation.rotated270)
            {
                rotateMode = RotateMode.Rotate270;
                flipMode = FlipMode.None;
                return true;
            }
            if (orientation == Orientation.rotated270Mirrored)
            {
                rotateMode = RotateMode.Rotate270;
                flipMode = FlipMode.Horizontal;
                return true;
            }

            rotateMode = RotateMode.None;
            flipMode = FlipMode.None;
            return false;
        }

        public static DateTime? ExifTaken(this Image image)
        {
            return image.Metadata.ExifProfile.Values
                .First(
                    (item, next) =>
                    {
                        if (item.Tag == ExifTag.DateTime)
                            return ParseDateTime();
                        if (item.Tag == ExifTag.DateTimeDigitized)
                            return ParseDateTime();
                        if (item.Tag == ExifTag.DateTimeOriginal)
                            return ParseDateTime();

                        return next();

                        DateTime? ParseDateTime()
                        {
                            if (item.DataType != ExifDataType.Ascii)
                                return next();
                            var value = (string)item.GetValue();

                            if (DateTime.TryParseExact(value, "yyyy:MM:dd HH:mm:ss",
                                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateTaken))
                                return dateTaken;

                            return next();
                        }
                    },
                    () => default(DateTime?));
        }

        public static double? ExifLat(this Image image)
        {
            var exifData = image.Metadata.ExifProfile.Values;

            return ExtractCoordinate(exifData,
                    ExifTag.GPSLatitude, ExifTag.GPSLatitudeRef,
                (location) => location,
                () => ExtractCoordinate(exifData,
                        ExifTag.GPSDestLatitude, ExifTag.GPSDestLatitudeRef,
                    (location) => location,
                    () => default(double?)));
        }

        public static double? ExifLon(this Image image)
        {
            var exifData = image.Metadata.ExifProfile.Values;

            return ExtractCoordinate(exifData,
                    ExifTag.GPSLongitude, ExifTag.GPSLongitudeRef,
                (location) => location,
                () => ExtractCoordinate(exifData,
                        ExifTag.GPSDestLongitude, ExifTag.GPSDestLongitudeRef,
                    (location) => location,
                    () => default(double?)));
        }

        private static TResult ExtractCoordinate<TResult>(
                IEnumerable<IExifValue> exifData,
                ExifTag tagCoordinate, ExifTag tagRef,
            Func<double, TResult> onParsed,
            Func<TResult> onFailedToParse)
        {
            return exifData.Contains(
                item => item.Tag == tagCoordinate,
            (location) => exifData.Contains(
                    item => item.Tag == tagRef,
                reference =>
                {
                    return ParseCoordinate(location, reference,
                        v => onParsed(v),
                        () => onFailedToParse());
                },
                () => onFailedToParse()),
            () => onFailedToParse());
        }

        private static TResult ParseCoordinate<TResult>(IExifValue location, IExifValue reference,
            Func<double, TResult> onParsed,
            Func<TResult> onFailedToParse)
        {
            if (location.DataType != ExifDataType.Rational)
                return onFailedToParse();

            var value = (SixLabors.ImageSharp.Rational[])location.GetValue();
            if (!value.Any())
                return onFailedToParse();

            var deg = ToDouble(value[0]);
            var min = ToDouble(value[1]) / ((double)60);
            var sec = ToDouble(value[2]) / ((double)3600);

            var locationNoRef = deg + min + sec;

            if (reference.DataType != ExifDataType.Ascii)
                return onFailedToParse();

            var valueRef = (string)reference.GetValue();
            var directionalMultiplier = IsWestOrSouth() ?
                -1.0
                :
                1.0;

            var locationWithRef = locationNoRef * directionalMultiplier;
            return onParsed(locationWithRef);

            double ToDouble(SixLabors.ImageSharp.Rational n)
            {
                var num = (double)n.Numerator;
                var den = (double)n.Denominator;
                return num / den;
            }

            bool IsWestOrSouth()
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
