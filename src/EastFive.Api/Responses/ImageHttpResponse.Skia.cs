using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http.Headers;

using SkiaSharp;

using EastFive.Images;

namespace EastFive.Api
{
    /// <summary>
    /// An image resized on construction and encoded on write. The encoding is
    /// <c>contentType.ParseImageEncoder()</c>: PNG, JPEG or WEBP, with PNG for anything else, and
    /// the Content-Type header names what is actually written.
    /// </summary>
    public class SkiaImageHttpResponse : HttpResponse
    {
        protected SKBitmap newImage;
        protected SKEncodedImageFormat encoder;
        private string fileName;
        private bool? inline;

        public SkiaImageHttpResponse(IHttpRequest request, HttpStatusCode statusCode,
            SKBitmap image,
            int? width = default(int?), int? height = default(int?), bool? fill = default,
            string fileName = default, string contentType = default, bool? inline = default)
            : base(request, statusCode)
        {
            this.fileName = fileName;
            this.inline = inline;
            this.encoder = contentType.ParseImageEncoder();

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

            this.newImage = image.Scale(newWidth, newHeight);
        }

        public override void WriteHeaders(Microsoft.AspNetCore.Http.HttpContext context, ResponseHeaders headers)
        {
            headers.SetFileHeaders(this.fileName, encoder.ToMimeType(), this.inline);
        }

        public override async Task WriteResponseAsync(Stream responseStream)
        {
            await this.newImage.SaveWithQualityAsync(responseStream, this.encoder);
            await responseStream.FlushAsync();
        }

    }
}
