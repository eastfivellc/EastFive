using EastFive.Api.Core;
using EastFive.Api.Serialization;
using EastFive.Extensions;
using EastFive.Web;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace EastFive.Api
{
    public interface IBindXmlApiValue
    {
        TResult ParseContentDelegate<TResult>(
                XmlDocument xmlDoc, string rawContent,
                ParameterInfo parameterInfo,
                IApplication httpApp, IHttpRequest request,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure);
    }

    public class XmlContentParserAttribute : Attribute, IParseContent
    {
        public bool DoesParse(IHttpRequest request)
        {
            return request.IsXml();
        }

        public async Task<IHttpResponse> ParseContentValuesAsync(
            IApplication httpApp, IHttpRequest request,
            Func<
                CastDelegate, 
                string[],
                Task<IHttpResponse>> onParsedContentValues)
        {
            if (!request.HasBody)
                return await BodyMissing("Body was not provided");

            var contentString = await request.ReadContentAsStringAsync();

            if (contentString.IsNullOrWhiteSpace())
                return await BodyMissing("XML body content was empty");
            
            var xmldoc = new XmlDocument();
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore, // prevents XXE attacks, such as Billion Laughs
                MaxCharactersFromEntities = 1024,
                XmlResolver = null,                   // prevents external entity DoS attacks, such as slow loading links or large file requests
            };
            try
            {
                using (var strReader = new StringReader(contentString))
                using (var xmlReader = XmlReader.Create(strReader, settings))
                {
                    xmldoc.Load(xmlReader);
                }
            }
            catch (Exception ex)
            {
                return await BodyMissing(ex.Message);
            }

            CastDelegate parser =
                (paramInfo, onParsed, onFailure) =>
                {
                    return paramInfo
                        .GetAttributeInterface<IBindXmlApiValue>()
                        .ParseContentDelegate(xmldoc, contentString,
                            paramInfo, httpApp, request,
                            onParsed,
                            onFailure);
                };
            return await onParsedContentValues(parser, new string[] { });


            Task<IHttpResponse> BodyMissing(string failureMessage)
            {
                CastDelegate emptyParser =
                    (paramInfo, onParsed, onFailure) =>
                    {
                        var key = paramInfo
                            .GetAttributeInterface<IBindApiValue>()
                            .GetKey(paramInfo)
                            .ToLower();
                        var type = paramInfo.ParameterType;
                        return onFailure($"XML [{key}] could not be parsed ({failureMessage}).");
                    };
                var exceptionKeys = new string[] { };
                return onParsedContentValues(emptyParser, exceptionKeys);
            }
        }
    }
}
