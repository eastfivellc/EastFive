using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;

using EastFive.Linq;
using System.Runtime.Versioning;

namespace EastFive.Images
{
    public class ComparisonSignature
    {
        private Bitmap thumbprintImage;

        public ComparisonSignature(Image thumbprintImage)
        {
            this.thumbprintImage = (thumbprintImage as Bitmap);
        }

        public double Compare(ComparisonSignature comparisonSignature)
        {
            if (!OperatingSystem.IsWindows())
                throw new NotSupportedException("OS not supported");

            #pragma warning disable CA1416
            var totalDelta = Enumerable.Range(0, 9)
                .SelectMany(
                    x =>
                    {
                        return Enumerable.Range(0, 9)
                            .Select(
                                y =>
                                {
                                    var pixel1 = thumbprintImage.GetPixel(x, y);
                                    var pixel2 = comparisonSignature
                                        .thumbprintImage.GetPixel(x, y);
                                    var totalColorDelta = Math.Abs(pixel1.R - pixel2.R)
                                        + Math.Abs(pixel1.G - pixel2.G)
                                        + Math.Abs(pixel1.B - pixel2.B);
                                    return totalColorDelta / (255.0 * 3.0);
                                });
                    })
                .Sum();
            return totalDelta / 9.0;
            #pragma warning restore CA1416
        }
    }

    [SupportedOSPlatform("windows6.1")]
    public static class ComparisonExtensions
    {
        public static ComparisonSignature GenerateComparisonSignature(this Image image)
        {
            var thumbprintImage = image.Scale(9, 9);
            return new ComparisonSignature(thumbprintImage);
        }
    }
}
