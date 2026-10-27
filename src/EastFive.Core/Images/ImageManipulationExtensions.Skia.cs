using System;

using SkiaSharp;

namespace EastFive.Images
{
    /// <summary>
    /// Resize, crop and aspect operations on a decoded bitmap. Each one MUTATES the bitmap it is
    /// called on and returns that same instance, as the ImageSharp versions these replace did, so
    /// a caller that ignores the return value still sees the change.
    /// </summary>
    public static class SkiaManipulationExtensions
    {
        private static readonly SKSamplingOptions Sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);

        public static SKBitmap ResizeImage(this SKBitmap image,
            int? width = default(int?), int? height = default(int?))
        {
            if (!width.HasValue)
                if (!height.HasValue)
                    return image;

            var ratio = ((double)image.Width) / ((double)image.Height);
            var newWidth = (int)Math.Round(width.HasValue ?
                    width.Value
                    :
                    height.HasValue ?
                        height.Value * ratio
                        :
                        image.Width);
            var newHeight = (int)Math.Round(height.HasValue ?
                    height.Value
                    :
                    width.HasValue ?
                        width.Value / ratio
                        :
                        image.Width);

            return image.Resize(newWidth, newHeight);
        }

        /// <summary>
        /// Keeps the region (<paramref name="x"/>, <paramref name="y"/>, <paramref name="w"/>,
        /// <paramref name="h"/>). A region reaching outside the image throws
        /// <see cref="ArgumentException"/>; it is not clipped or padded.
        /// </summary>
        public static SKBitmap Crop(this SKBitmap image, int x, int y, int w, int h)
        {
            var region = SKRectI.Create(x, y, w, h);
            if (w <= 0 || h <= 0 || x < 0 || y < 0 || region.Right > image.Width || region.Bottom > image.Height)
                throw new ArgumentException(
                    "Crop rectangle should be smaller than the source bounds.", "cropRectangle");

            var cropped = new SKBitmap(new SKImageInfo(w, h, image.ColorType, image.AlphaType));
            using (var canvas = new SKCanvas(cropped))
                canvas.DrawBitmap(image, region, SKRect.Create(w, h), new SKSamplingOptions(SKFilterMode.Nearest), null);
            return image.ReplaceWith(cropped);
        }

        public static SKBitmap Crop(this SKBitmap image, double x, double y, double w, double h)
        {
            return image.Crop(
                (int)(x * image.Width),
                (int)(y * image.Height),
                (int)(w * image.Width),
                (int)(h * image.Height));
        }

        public static SKBitmap Scale(this SKBitmap image,
            int? width = default(int?), int? height = default(int?), bool? fill = default(bool?))
        {
            bool KeepOriginal()
            {
                if (width.HasValue || height.HasValue || fill.HasValue)
                    return false;
                return true;
            }
            if (KeepOriginal())
                return image;

            var ratio = ((double)image.Width) / ((double)image.Height);
            var newWidth = (int)Math.Round(width.HasValue ?
                    width.Value
                    :
                    height.HasValue ?
                        height.Value * ratio
                        :
                        image.Width);
            var newHeight = (int)Math.Round(height.HasValue ?
                    height.Value
                    :
                    width.HasValue ?
                        width.Value / ratio
                        :
                        image.Width);

            return image.Resize(newWidth, newHeight);
        }

        public static SKBitmap MaxAspect(this SKBitmap image, double viewportAspect)
        {
            if (!image.ComputeMaxAspect(viewportAspect,
                    out int x, out int y, out int width, out int height))
                return image;

            return image.Crop(x, y, width, height);
        }

        public static bool ComputeMaxAspect(this SKBitmap image, double viewportAspect,
            out int xOffset, out int yOffset, out int width, out int height)
        {
            return ComputeMaxAspect(viewportAspect, image.Width, image.Height,
                out xOffset, out yOffset, out width, out height);
        }

        public static bool ComputeMaxAspect(double viewportAspect,
            int imageWidth, int imageHeight,
            out int xOffset, out int yOffset, out int width, out int height)
        {
            var widthD = (double)imageWidth;
            var heightD = (double)imageHeight;
            double imageAspect = widthD / heightD;

            if (imageAspect < viewportAspect)
            {
                var newHeight = imageWidth / viewportAspect;
                var heightCrop = (imageHeight - newHeight) + 1.0;
                xOffset = 0;
                yOffset = (int)(heightCrop / 2);
                width = imageWidth;
                height = (int)(newHeight + 0.5);
                return true;
            }

            if (imageAspect > viewportAspect)
            {
                var exactWidth = imageHeight * viewportAspect;
                var widthCrop = imageWidth - exactWidth;
                xOffset = (int)(widthCrop / 2);
                yOffset = 0;
                width = (int)(exactWidth + 0.5);
                height = imageHeight;
                return true;
            }

            xOffset = 0;
            yOffset = 0;
            width = imageWidth;
            height = imageHeight;
            return false;
        }

        /// <summary>
        /// Crops to <paramref name="viewportAspect"/> by asking for a region LARGER than the
        /// image, which <see cref="Crop(SKBitmap, int, int, int, int)"/> rejects. Kept as it was:
        /// it reads like a letterbox but never was one.
        /// </summary>
        public static SKBitmap MinAspect(this SKBitmap image, double viewportAspect)
        {
            var imageAspect = image.AspectRatio();
            if (imageAspect > viewportAspect)
            {
                var newHeight = image.Width / viewportAspect;
                var heightMargin = (newHeight - image.Height) + 1.0;
                var newY = (int)(heightMargin / 2);
                return image.Crop(0, newY, image.Width, (int)(newHeight + 0.5));
            }
            if (imageAspect < viewportAspect)
            {
                var newWidth = image.Height * viewportAspect;
                var widthMargin = newWidth - image.Width;
                var newX = (int)(widthMargin / 2);
                return image.Crop(newX, 0, (int)(newWidth + 0.5), image.Height);
            }
            return image;
        }

        public static SKBitmap ScaleAspect(this SKBitmap image, double viewportAspect)
        {
            var imageAspect = image.AspectRatio();
            if (imageAspect > viewportAspect)
            {
                var newHeight = image.Width / viewportAspect;
                return image.Scale(image.Width, (int)(newHeight + 0.5), true);
            }
            if (imageAspect < viewportAspect)
            {
                var newWidth = image.Height * viewportAspect;
                return image.Scale((int)(newWidth + 0.5), image.Height, true);
            }
            return image;
        }

        public static double AspectRatio(this SKBitmap image)
        {
            var width = (double)image.Width;
            var height = (double)image.Height;
            return width / height;
        }

        private static SKBitmap Resize(this SKBitmap image, int width, int height)
        {
            var resized = image.Resize(new SKImageInfo(width, height, image.ColorType, image.AlphaType), Sampling)
                ?? throw new InvalidOperationException($"Skia could not resize the image to {width}x{height}.");
            return image.ReplaceWith(resized);
        }

        /// <summary>
        /// Copies <paramref name="replacement"/> into <paramref name="image"/> (CopyTo reallocates
        /// the destination at the replacement's size) and disposes it, which is what keeps these
        /// operations in place. <c>SKBitmap.Swap</c> would avoid the copy but is not public.
        /// </summary>
        internal static SKBitmap ReplaceWith(this SKBitmap image, SKBitmap replacement)
        {
            using (replacement)
            {
                if (!replacement.CopyTo(image))
                    throw new InvalidOperationException("Skia could not copy the transformed pixels back into the image.");
                return image;
            }
        }
    }
}
