using EastFive.Api.Bindings;
using EastFive.Api.Serialization;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Linq.Async;
using EastFive.Web;
using EastFive.Api.Core;
using EastFive.Linq;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;

namespace EastFive.Api
{
    public interface IBindMultipartApiValue
    {
        TResult ParseContentDelegate<TResult>(IDictionary<string, MultipartContentTokenParser> content,
                ParameterInfo parameterInfo,
                IApplication httpApp, IHttpRequest routeData,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure);
    }

    public class MimeMultipartContentParserAttribute : Attribute, IParseContent
    {
        public bool DoesParse(IHttpRequest request)
        {
            if (!request.HasFormContentType)
                return false;
            return request.IsMimeMultipartContent();
        }

        public async Task<IHttpResponse> ParseContentValuesAsync(
            IApplication httpApp, IHttpRequest request,
            Func<
                CastDelegate, 
                string[],
                Task<IHttpResponse>> onParsedContentValues)
        {
            var contentsLookup = await request.Form.Files
                .Select(
                    async (file) =>
                    {
                        if (file.IsDefaultOrNull())
                            return default;

                        var key = file.Name;
                        var fileNameMaybe = file.FileName;
                        if (null != fileNameMaybe)
                            fileNameMaybe = fileNameMaybe.Trim(new char[] { '"' });
                        var contents = await file.OpenReadStream().ToBytesAsync();

                        var kvp = key.PairWithValue(
                            new MultipartContentTokenParser(file, contents, fileNameMaybe));
                        return kvp.AsOptional();
                    })
                .AsyncEnumerable()
                .SelectWhereHasValue()
                .ToDictionaryAsync();

            CastDelegate parser =
                (paramInfo, onParsed, onFailure) =>
                {
                    return paramInfo
                        .GetAttributeInterface<IBindMultipartApiValue>(true)
                        .ParseContentDelegate(contentsLookup,
                                paramInfo, httpApp, request,
                            onParsed,
                            onFailure);
                };
            return await onParsedContentValues(parser, contentsLookup.SelectKeys().ToArray());
        }

    }

    public class MultipartContentTokenParser : IParseToken
    {
        private byte[] contents;
        private string fileNameMaybe;
        private IFormFile file;

        public class MemoryStreamForFile : MemoryStream
        {
            public MemoryStreamForFile(byte[] buffer) : base(buffer) { }
            public string FileName { get; set; }
        }

        public MultipartContentTokenParser(IFormFile file, byte[] contents, string fileNameMaybe)
        {
            this.file = file;
            this.contents = contents;
            this.fileNameMaybe = fileNameMaybe;
        }

        public IParseToken[] ReadArray()
        {
            throw new NotImplementedException();
        }

        public byte[] ReadBytes()
        {
            return contents;
        }

        public IDictionary<string, IParseToken> ReadDictionary()
        {
            throw new NotImplementedException();
        }

        public T ReadObject<T>()
        {
            if (typeof(HttpContent) == typeof(T))
            {
                return (T)((object)this.file);
            }
            //if (typeof(ByteArrayContent) == typeof(T))
            //{
            //    if(this.file is ByteArrayContent)
            //        return (T)((object)this.file);
            //    var byteArrayContent = new ByteArrayContent(this.file.)
            //    {
            //        Headers
            //    }
            //}
            throw new NotImplementedException();
        }

        public object ReadObject()
        {
            throw new NotImplementedException();
        }

        public Stream ReadStream()
        {
            return new MemoryStreamForFile(contents)
            { FileName = fileNameMaybe };
        }

        public string ReadString()
        {
            return System.Text.Encoding.UTF8.GetString(contents);
        }
    }
}
