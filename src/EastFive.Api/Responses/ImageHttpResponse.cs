using EastFive.Extensions;
using EastFive.Images;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class ImageHttpResponse : HttpResponse
    {
        protected Bitmap newImage;
        protected ImageCodecInfo encoder;

        public ImageHttpResponse(IHttpRequest request, HttpStatusCode statusCode,
            Image image,
            int? width = default(int?), int? height = default(int?), bool? fill = default,
            string fileName = default, string contentType = default, bool? inline = default)
            : base(request, statusCode)
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
                throw new NotSupportedException("OS not supported");

            if (!TryGetEncoderInfo(contentType, out encoder))
                TryGetEncoderInfo("image/jpeg", out encoder);
            this.SetFileHeaders(fileName, encoder.MimeType, inline);

            image.FixOrientation();
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

            newImage = new Bitmap(newWidth, newHeight, PixelFormat.Format32bppArgb);

            //set the new resolution
            newImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            //start the resizing
            using (var graphics = System.Drawing.Graphics.FromImage(newImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                var brush = System.Drawing.Brushes.White;
                graphics.FillRectangle(brush, 0, 0, newWidth, newHeight);

                //set some encoding specs
                graphics.CompositingMode = CompositingMode.SourceOver;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                graphics.DrawImage(image, 0, 0, newWidth, newHeight);
            }
        }

        public override async Task WriteResponseAsync(Stream responseStream)
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
                throw new NotSupportedException("OS not supported");

            var encoderParameters = new EncoderParameters(1);
            encoderParameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 80L);

            using (var memoryStream = new MemoryStream())
            {
                newImage.Save(memoryStream, encoder, encoderParameters);
                await memoryStream.FlushAsync();
                var bytes = memoryStream.ToArray();
                await responseStream.WriteAsync(bytes, 0, bytes.Length);
                await responseStream.FlushAsync();
                //responseStream.Close();
            }
        }

        private static bool TryGetEncoderInfo(string mimeType, out ImageCodecInfo encoder)
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
                throw new NotSupportedException("OS not supported");

            if (mimeType.IsNullOrWhiteSpace())
            {
                encoder = default;
                return false;
            }
            ImageCodecInfo[] encoders = ImageCodecInfo.GetImageEncoders();

            for (int j = 0; j < encoders.Length; ++j)
            {
                if (encoders[j].MimeType.ToLower() == mimeType.ToLower())
                {
                    encoder =  encoders[j];
                    return true;
                }
            }

            encoder = default;
            return false;
        }
    }
}
