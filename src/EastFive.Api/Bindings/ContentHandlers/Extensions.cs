using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using EastFive;
using EastFive.Extensions;

namespace EastFive.Api.Bindings.ContentHandlers
{
    public static class Extensions
    {
        public static async Task WriteResponseText(this Stream responseStream,
            string content, IHttpRequest request)
        {
            using (var streamWriter = request.TryGetAcceptCharset(out Encoding writerEncoding) ?
                new StreamWriter(responseStream, writerEncoding)
                :
                new StreamWriter(responseStream, new UTF8Encoding(false)))
            {
                await streamWriter.WriteAsync(content);
                await streamWriter.FlushAsync();
            }
        }

        public static async Task WriteResponseText(this Stream responseStream,
            string content, Encoding encoding)
        {
            using (var streamWriter = encoding.IsDefaultOrNull() ?
                new StreamWriter(responseStream, new UTF8Encoding(false))
                :
                new StreamWriter(responseStream, encoding))
            {
                await streamWriter.WriteAsync(content);
                await streamWriter.FlushAsync();
            }


            //var writer = encoding.IsDefaultOrNull() ?
            //    new StreamWriter(responseStream)
            //    :
            //    new StreamWriter(responseStream, encoding);
            //await writer.WriteAsync(contentJsonString);
        }
    }
}
