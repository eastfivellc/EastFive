using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EastFive.Extensions;
using EastFive.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace EastFive.Images
{
    public static class ImageSharpManipulationExtensions
    {
        public static Image ResizeImage(this Image image,
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


            image.Mutate(x => x.Resize(newWidth, newHeight));
            return image;
        }

        public static Image Crop(this Image image, int x, int y, int w, int h)
        {
            var cropRegion = new Rectangle(x, y, w, h);
            image.Mutate(x => x.Crop(cropRegion));
            return image;
        }

        public static Image Crop(this Image image, double x, double y, double w, double h)
        {
            return image.Crop(
                (int)(x * image.Width),
                (int)(y * image.Height),
                (int)(w * image.Width),
                (int)(h * image.Height));
        }

        public static Image Scale(this Image image,
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

            image.Mutate(x => x.Resize(newWidth, newHeight));
            return image;
        }

        public static Image MaxAspect(this Image image, double viewportAspect)
        {
            if (!image.ComputeMaxAspect(viewportAspect, 
                    out int x, out int y, out int width, out int height))
                return image;

            return image.Crop(x, y, width, height);
        }

        public static bool ComputeMaxAspect(this Image image, double viewportAspect,
            out int xOffset, out int yOffset, out int width, out int height)
        {
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

        public static Image MinAspect(this Image image, double viewportAspect)
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

        public static Image ScaleAspect(this Image image, double viewportAspect)
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

        public static double AspectRatio(this Image image)
        {
            var width = (double)image.Width;
            var height = (double)image.Height;
            return width / height;
        }

    }
}
