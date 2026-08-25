using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http.Headers;

using EastFive;
using EastFive.Extensions;
using EastFive.Reflection;
using EastFive.Linq.Async;
using EastFive.Serialization;

namespace EastFive.Api
{
    public class EnumerableAsyncCsvResponse<T> : HttpResponse
    {
        private IEnumerableAsync<T> objectsAsync;
        private IApplication application;
        private ParameterInfo parameterInfo;
        private string fileName;
        private bool includeHeaders;
        private bool inline;

        public EnumerableAsyncCsvResponse(IApplication application,
                IHttpRequest request, ParameterInfo parameterInfo, HttpStatusCode statusCode,
                IEnumerableAsync<T> objectsAsync, string fileName, bool includeHeaders, bool inline)
            : base(request, statusCode)
        {
            this.application = application;
            this.objectsAsync = objectsAsync;
            this.parameterInfo = parameterInfo;
            this.fileName = fileName;
            this.inline = inline;
            this.includeHeaders = includeHeaders;
        }

        public override void WriteHeaders(Microsoft.AspNetCore.Http.HttpContext context, ResponseHeaders headers)
        {
            base.WriteHeaders(context, headers);
            headers.SetFileHeaders(fileName, "text/csv", inline);
        }

        public override async Task WriteResponseAsync(Stream responseStream)
        {
            using (var streamWriter =
                this.Request.TryGetAcceptCharset(out Encoding writerEncoding) ?
                    new StreamWriter(responseStream, writerEncoding)
                    :
                    new StreamWriter(responseStream, new UTF8Encoding(false)))
            {
                var enumerator = objectsAsync.GetEnumerator();
                
                try
                {
                    var castings = typeof(T)
                        .GetPropertyAndFieldsWithAttributesInterface<ICast<string>>()
                        .ToArray();

                    bool first = true;
                    if (includeHeaders)
                    {
                        var headerCsvStrings = castings
                            .Select(
                                tpl => tpl.Item1.TryGetAttributeInterface(out IProvideApiValue apiValueProvider) ?
                                    apiValueProvider.GetPropertyName(tpl.Item1).Replace('_', ' ')
                                    :
                                    " ")
                            .Join(",");
                        await streamWriter.WriteAsync(headerCsvStrings);
                        await streamWriter.FlushAsync();

                        first = false;
                    }

                    while (await enumerator.MoveNextAsync())
                    {
                        if (!first)
                        {
                            await streamWriter.WriteAsync('\r');
                            await streamWriter.FlushAsync();
                        }
                        first = false;

                        var obj = enumerator.Current;

                        var contentCsvStrings = castings
                            .Select(
                                tpl => tpl.Item2.Cast(
                                        tpl.Item1.GetPropertyOrFieldValue(obj),
                                        tpl.Item1.GetPropertyOrFieldType(),
                                        String.Empty,
                                        tpl.Item1,
                                    value => value,
                                    () => string.Empty))
                            .Join(",");
                        await streamWriter.WriteAsync(contentCsvStrings);
                        await streamWriter.FlushAsync();
                        
                    }
                }
                catch (Exception ex)
                {
                    await streamWriter.WriteAsync(ex.Message);
                }
                finally
                {
                    await streamWriter.FlushAsync();
                }
            }
        }
    }
}
