using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

using Microsoft.AspNetCore.Http;

using Newtonsoft.Json.Linq;

using EastFive.Api.Bindings;
using EastFive.Api.Serialization;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Web;
using EastFive.Linq;

namespace EastFive.Api
{
    public interface IBindFormDataApiValue
    {
        TResult ParseContentDelegate<TResult>(IFormCollection formData,
                ParameterInfo parameterInfo,
                IApplication httpApp, IHttpRequest request,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure);
    }

    public class FormDataParserAttribute : Attribute, IParseContent
    {
        public bool DoesParse(IHttpRequest request)
        {
            if (!request.HasFormContentType)
                return false;
            if (request.Form.Any())
                return true;
            if (request.Form.Files.Any())
                return true;
            return false;
        }

        public async Task<IHttpResponse> ParseContentValuesAsync(
            IApplication httpApp, IHttpRequest request,
            Func<
                CastDelegate, 
                string[],
                Task<IHttpResponse>> onParsedContentValues)
        {
            var formData = request.Form;

            var parameters = formData.SelectKeys().ToArray();
            CastDelegate parser =
                (paramInfo, onParsed, onFailure) =>
                {
                    return paramInfo
                        .GetAttributesInterface<IBindFormDataApiValue>(true)
                        .First(
                            (formBinder, next) => formBinder.ParseContentDelegate(formData, 
                                    paramInfo, httpApp, request,
                                onParsed,
                                onFailure),
                            () => onFailure($"{paramInfo.Name} does not handle form data."));
                };
            return await onParsedContentValues(parser, parameters);
        }
    }
}
