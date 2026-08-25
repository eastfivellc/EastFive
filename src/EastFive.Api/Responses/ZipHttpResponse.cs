using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using EastFive.Linq.Async;

namespace EastFive.Api
{
    public class ZipHttpResponse : EastFive.Api.HttpResponse
    {
        private IEnumerable<(FileInfo, byte[])> files;

        public ZipHttpResponse(IHttpRequest request,
            string fileName, bool? inline,
            IEnumerable<(FileInfo, byte[])> files)
            : base(request, HttpStatusCode.OK)
        {
            this.SetFileHeaders(fileName, "application/zip", inline);
            this.files = files;
        }

        public override async Task WriteResponseAsync(Stream responseStream)
        {
            using (var syncStream = new StreamAsyncWrapper(responseStream))
            {
                using (var archive = new ZipArchive(syncStream, ZipArchiveMode.Create, true))
                {
                    foreach (var file in files)
                    {
                        var fileBytes = file.Item2;
                        var fileName = file.Item1.Name;
                        var zipArchiveEntry = archive.CreateEntry(
                            fileName, CompressionLevel.Fastest);
                        using (var zipStream = zipArchiveEntry.Open())
                            await zipStream.WriteAsync(fileBytes, 0, fileBytes.Length);
                    }
                }
                await syncStream.CompleteAsync();
            }
        }
    }

    public class ZipAsyncHttpResponse : EastFive.Api.HttpResponse
    {
        private IEnumerableAsync<(string, Func<Stream, Task>)> files;

        public ZipAsyncHttpResponse(IHttpRequest request,
            string fileName, bool? inline,
            IEnumerableAsync<(string, Func<Stream, Task>)> files)
            : base(request, HttpStatusCode.OK)
        {
            this.SetFileHeaders(fileName, "application/zip", inline);
            this.files = files;
        }

        public override async Task WriteResponseAsync(Stream responseStream)
        {
            using (var syncStream = new StreamAsyncWrapper(responseStream))
            {
                using (var archive = new ZipArchive(syncStream, ZipArchiveMode.Create, true))
                {
                    var enumerator = files.GetEnumerator();
                    while (await enumerator.MoveNextAsync())
                    {
                        var (fileName, dataWriteAsync) = enumerator.Current;
                        var zipArchiveEntry = archive.CreateEntry(
                            fileName, CompressionLevel.Fastest);
                        using (var zipStream = zipArchiveEntry.Open())
                        {
                            await dataWriteAsync(zipStream);
                            await zipStream.FlushAsync();
                        }
                        await syncStream.FlushAsync();
                    }
                }
                await syncStream.CompleteAsync();
            }
        }
    }
}
