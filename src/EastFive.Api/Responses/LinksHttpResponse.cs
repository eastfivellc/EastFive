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
    public class LinksHttpResponse : EastFive.Api.HttpResponse
    {
        private IEnumerable<Uri> links;

        public LinksHttpResponse(IHttpRequest request,
            IEnumerable<Uri> links)
            : base(request, HttpStatusCode.OK)
        {
            this.links = links;
        }

        public override async Task WriteResponseAsync(Stream responseStream)
        {
            var bytesToWritePreamble = $"<html><head><style></style></head><body><ul>".GetBytes();
            await responseStream.WriteAsync(bytesToWritePreamble);
            foreach (var link in links)
            {
                var img = $"<li><a href=\"{link.OriginalString}\">{link}</a></li>";
                var bytesToWrite = img.GetBytes();
                await responseStream.WriteAsync(bytesToWrite);
            }
            var bytesToWriteEnd = "</ul></body></html>".GetBytes();
            await responseStream.WriteAsync(bytesToWriteEnd);
        }
    }
}
