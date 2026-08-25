using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

using EastFive;
using EastFive.Extensions;
using EastFive.Graphics;
using EastFive.Images;
using EastFive.Serialization;

namespace EastFive.Api
{
    public class LinkedGalleryHttpResponse : EastFive.Api.HttpResponse
    {
        private IEnumerable<(Image, Uri)> images;
        private string mimeType;
        private int? imagesPerLine;

        public LinkedGalleryHttpResponse(IHttpRequest request,
            IEnumerable<(Image, Uri)> images,
            string mimeType, int? imagesPerLine)
            : base(request, HttpStatusCode.OK)
        {
            this.mimeType = mimeType;
            this.images = images;
            this.imagesPerLine = imagesPerLine;
        }

        public override async Task WriteResponseAsync(Stream responseStream)
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
                throw new NotSupportedException("OS not supported");

            var css = (imagesPerLine.HasValue && imagesPerLine.Value > 0) ?
                $"img {{width: {100 / imagesPerLine.Value}%;}}"
                :
                "";
            var bytesToWritePreamble = $"<html><head><style>{css}</style></head><body>".GetBytes();
            await responseStream.WriteAsync(bytesToWritePreamble);
            foreach (var image in images)
            {
                var bytes = image.Item1.Save(out ImageCodecInfo codecUsed, encodingMimeType:mimeType);
                image.Item1.Dispose();
                var base64 = bytes.ToBase64String();
                var src = $"data:{codecUsed.MimeType};base64,{base64}";
                var img = $"<a href=\"{image.Item2}\"><img src=\"{src}\" /></a>";
                var bytesToWrite = img.GetBytes();
                await responseStream.WriteAsync(bytesToWrite);
            }
            var bytesToWriteEnd = "</body></html>".GetBytes();
            await responseStream.WriteAsync(bytesToWriteEnd);
        }
    }
}
