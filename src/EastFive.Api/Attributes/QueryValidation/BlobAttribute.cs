using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Api.Serialization;
using EastFive.Reflection;
using EastFive.Api.Bindings;
using EastFive.Api.Extensions;

namespace EastFive.Api
{
    public class BlobAttribute : System.Attribute, 
        IBindApiValue, IBindMultipartApiValue, IBindFormDataApiValue
    {
        public string PropertyName { get; set; }

        public string GetKey(ParameterInfo paramInfo)
        {
            if(PropertyName.HasBlackSpace())
                return this.PropertyName;
            return paramInfo.Name;
        }

        public virtual SelectParameterResult TryCast(BindingData bindingData)
        {
            var parameterRequiringValidation = bindingData.parameterRequiringValidation;
            var fileKey = GetKey(parameterRequiringValidation);
            return bindingData.fetchBodyParam(parameterRequiringValidation,
                (value) => SelectParameterResult.Body(value, fileKey, parameterRequiringValidation),
                (why) => SelectParameterResult.FailureBody(why, fileKey, parameterRequiringValidation));
        }

        public virtual TResult ParseContentDelegate<TResult>(
                IDictionary<string, MultipartContentTokenParser> content, 
                ParameterInfo parameterInfo, 
                IApplication httpApp, IHttpRequest request, 
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            if (parameterInfo.TryGetAttributeInterfaceFromChain(httpApp, out IProvideBlobValue blobValueProvider))
                return onFailure($"Could not identify an {nameof(IProvideBlobValue)} attribute on {parameterInfo.Member.DeclaringType.FullName}..{parameterInfo.Member.Name}({parameterInfo.Name})");

            var fileKey = GetKey(parameterInfo);
            var valueToBind = content[fileKey];
            return blobValueProvider.ProvideValue(valueToBind,
                boundValue => onParsed(boundValue),
                why => onFailure(why));
        }

        public virtual TResult ParseContentDelegate<TResult>(IFormCollection formData,
                ParameterInfo parameterInfo,
                IApplication httpApp, IHttpRequest request,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            if (!parameterInfo.TryGetAttributeInterfaceFromChain(httpApp, out IProvideBlobValue blobValueProvider))
                return onFailure($"Could not identify an {nameof(IProvideBlobValue)} attribute on {parameterInfo.Member.DeclaringType.FullName}..{parameterInfo.Member.Name}({parameterInfo.Name})");

            var fileKey = GetKey(parameterInfo);
            var valueToBind = formData.Files[fileKey];
            if (valueToBind.IsDefaultOrNull())
                return onFailure($"`{fileKey}` not provided.");

            return blobValueProvider.ProvideValue(valueToBind,
                boundValue => onParsed(boundValue),
                why => onFailure(why));
        }
    }
}
