using EastFive.Api.Bindings;
using EastFive.Api.Resources;
using EastFive.Extensions;
using Newtonsoft.Json.Linq;
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
    public class FileNameAttribute : QueryValidationAttribute, IBindMultipartApiValue
    {
        public override SelectParameterResult TryCast(BindingData bindingData)
        {
            var parameterRequiringValidation = bindingData.parameterRequiringValidation;
            var key = this.GetKey(parameterRequiringValidation);
            return bindingData.fetchBodyParam(parameterRequiringValidation,
                vCasted => SelectParameterResult.Body(vCasted, key, parameterRequiringValidation),
                why => SelectParameterResult.FailureBody(why, key, parameterRequiringValidation));
        }

        public TResult ParseContentDelegate<TResult>(
                IDictionary<string, MultipartContentTokenParser> contentsLookup,
                ParameterInfo parameterInfo, IApplication httpApp, IHttpRequest request,
            Func<object, TResult> onParsed, 
            Func<string, TResult> onFailure)
        {
            var key = this.GetKey(parameterInfo);
            var type = parameterInfo.ParameterType;
            if (!contentsLookup.ContainsKey(key))
                return onFailure("Key not found");

            var tokenReader = contentsLookup[key];
            var content = tokenReader.ReadObject<HttpContent>();
            var header = content.Headers.ContentDisposition;

            if (type.IsAssignableFrom(typeof(System.Net.Http.Headers.ContentDispositionHeaderValue)))
                return onParsed((object)header);

            if (type.IsAssignableFrom(typeof(string)))
            {
                if (header.FileName.IsNullOrWhiteSpace())
                    return onParsed(header.FileName);
                var fileName = header.FileName.Trim().Trim(new char[] { '"', '\'' }).Trim();
                return onParsed(fileName);
            }

            return onFailure($"Cannot cast filename to {type.FullName}.");
        }
    }
}
