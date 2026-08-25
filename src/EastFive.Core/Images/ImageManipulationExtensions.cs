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
    public static class ImageManipulationExtensions
    {
        [SupportedOSPlatform("windows6.1")]
        public static Image ResizeImage(this Image image,
            int? width = default(int?), int? height = default(int?), bool? fill = default(bool?),
            Brush background = default)
        {
            if (!OperatingSystem.IsWindows())
                throw new NotSupportedException("OS not supported");

            if (!width.HasValue)
                if (!height.HasValue)
                    if (!fill.HasValue)
                        return image;
            
            var ratio = ((double)image.Size.Width) / ((double)image.Size.Height);
            var newWidth = (int)Math.Round(width.HasValue ?
                    width.Value
                    :
                    height.HasValue ?
                        height.Value * ratio
                        :
                        image.Size.Width);
            var newHeight = (int)Math.Round(height.HasValue ?
                    height.Value
                    :
                    width.HasValue ?
                        width.Value / ratio
                        :
                        image.Size.Width);

            var newImage = new Bitmap(newWidth, newHeight, PixelFormat.Format32bppArgb);

            //set the new resolution
            newImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            //start the resizing
            using (var graphics = System.Drawing.Graphics.FromImage(newImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                if(background.IsDefaultOrNull())
                    background = System.Drawing.Brushes.White;
                graphics.FillRectangle(background, 0, 0, newWidth, newHeight);

                //set some encoding specs
                graphics.CompositingMode = CompositingMode.SourceOver;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                graphics.DrawImage(image, 0, 0, newWidth, newHeight);
                return newImage;
            }
        }

        [SupportedOSPlatform("windows6.1")]
        public static Image SetBackground(this Image image,
            Brush background)
        {
            if (!OperatingSystem.IsWindows())
                throw new NotSupportedException("OS not supported");

            var newImage = new Bitmap(image.Width, image.Height, PixelFormat.Format24bppRgb);

            //set the new resolution
            newImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            //start the resizing
            using (var graphics = System.Drawing.Graphics.FromImage(newImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.FillRectangle(background, 0, 0, image.Width, image.Height);

                //set some encoding specs
                graphics.CompositingMode = CompositingMode.SourceOver;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                graphics.DrawImage(image, 0, 0, image.Width, image.Height);
                return newImage;
            }
        }

        [SupportedOSPlatform("windows6.1")]
        public static Image Crop(this Image image, int x, int y, int w, int h)
        {
            if (!OperatingSystem.IsWindows())
                throw new NotSupportedException("OS not supported");

            var newImage = new Bitmap(w, h, PixelFormat.Format32bppArgb);

            //set the new resolution
            newImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            //start the resizing
            using (var graphics = System.Drawing.Graphics.FromImage(newImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                var brush = System.Drawing.Brushes.Transparent;
                graphics.FillRectangle(brush, 0, 0, w, h);

                //set some encoding specs
                graphics.CompositingMode = CompositingMode.SourceOver;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                graphics.DrawImage(image, 0, 0,
                    new System.Drawing.Rectangle(x, y, w, h),
                    GraphicsUnit.Pixel);
            }
            return newImage;
        }

        [SupportedOSPlatform("windows6.1")]
        public static Image Crop(this Image image, double x, double y, double w, double h)
        {
            if (!OperatingSystem.IsWindows())
                throw new NotSupportedException("OS not supported");

            return image.Crop(
                (int)(x * image.Width),
                (int)(y * image.Height),
                (int)(w * image.Width),
                (int)(h * image.Height));
        }

        [SupportedOSPlatform("windows6.1")]
        public static Image Scale(this Image image,
            int? width = default(int?), int? height = default(int?), bool? fill = default(bool?))
        {
            if (!OperatingSystem.IsWindows())
                throw new NotSupportedException("OS not supported");

            bool KeepOriginal()
            {
                if (width.HasValue || height.HasValue || fill.HasValue)
                    return false;
                return true;
            }
            if (KeepOriginal())
                return image;

            var ratio = ((double)image.Size.Width) / ((double)image.Size.Height);
            var newWidth = (int)Math.Round(width.HasValue ?
                    width.Value
                    :
                    height.HasValue ?
                        height.Value * ratio
                        :
                        image.Size.Width);
            var newHeight = (int)Math.Round(height.HasValue ?
                    height.Value
                    :
                    width.HasValue ?
                        width.Value / ratio
                        :
                        image.Size.Width);

            var newImage = new Bitmap(newWidth, newHeight, PixelFormat.Format32bppArgb);

            //set the new resolution
            newImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            //start the resizing
            using (var graphics = System.Drawing.Graphics.FromImage(newImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                var brush = System.Drawing.Brushes.Transparent;
                graphics.FillRectangle(brush, 0, 0, newWidth, newHeight);

                //set some encoding specs
                graphics.CompositingMode = CompositingMode.SourceOver;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                graphics.DrawImage(image, 0, 0, newWidth, newHeight);
            }
            return newImage;
        }

        [SupportedOSPlatform("windows6.1")]
        public static Image MaxAspect(this Image image, double viewportAspect)
        {
            if (!image.ComputeMaxAspect(viewportAspect, 
                    out int x, out int y, out int width, out int height))
                return image;

            return image.Crop(x, y, width, height);
        }

        [SupportedOSPlatform("windows6.1")]
        public static bool ComputeMaxAspect(this Image image, double viewportAspect,
            out int xOffset, out int yOffset, out int width, out int height)
        {
            if (!OperatingSystem.IsWindows())
                throw new NotSupportedException("OS not supported");

            var imageWidth = image.Width;
            var imageHeight = image.Height;
            return ComputeMaxAspect(viewportAspect, imageWidth, imageHeight,
                out xOffset, out yOffset, out width, out height);
        }

        public static bool ComputeMaxAspect(double viewportAspect,
            int imageWidth, int imageHeight,
            out int xOffset, out int yOffset, out int width, out int height)
        {
            var widthD = (double)imageWidth;
            var heightD = (double)imageHeight;
            double imageAspect =  widthD / heightD;

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

        [SupportedOSPlatform("windows6.1")]
        public static Image MinAspect(this Image image, double viewportAspect)
        {
            if (!OperatingSystem.IsWindows())
                throw new NotSupportedException("OS not supported");

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

        [SupportedOSPlatform("windows6.1")]
        public static Image ScaleAspect(this Image image, double viewportAspect)
        {
            if (!OperatingSystem.IsWindows())
                throw new NotSupportedException("OS not supported");

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

        [SupportedOSPlatform("windows6.1")]
        public static double AspectRatio(this Image image)
        {
            if (!OperatingSystem.IsWindows())
                throw new NotSupportedException("OS not supported");

            var width = (double)image.Width;
            var height = (double)image.Height;
            return width / height;
        }

    }
}
