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
    public class BlobOptionalAttribute : BlobAttribute, 
        IBindApiValue, IBindMultipartApiValue, IBindFormDataApiValue
    {
        public override SelectParameterResult TryCast(BindingData bindingData)
        {
            var parameterRequiringValidation = bindingData.parameterRequiringValidation;
            var fileKey = GetKey(parameterRequiringValidation);
            return bindingData.fetchBodyParam(parameterRequiringValidation,
                (value) => SelectParameterResult.Body(value, fileKey, parameterRequiringValidation),
                (why) =>
                {
                    var result = SelectParameterResult.Body(null, fileKey, parameterRequiringValidation);
                    return result;
                    //return SelectParameterResult.FailureBody(null, fileKey, parameterRequiringValidation);
                });
        }

        public override TResult ParseContentDelegate<TResult>(
                IDictionary<string, MultipartContentTokenParser> content, 
                ParameterInfo parameterInfo, 
                IApplication httpApp, IHttpRequest request, 
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            if (parameterInfo.TryGetAttributeInterfaceFromChain(httpApp, out IProvideBlobValue blobValueProvider))
                return onFailure($"Could not identify an {nameof(IProvideBlobValue)} attribute on {parameterInfo.Member.DeclaringType.FullName}..{parameterInfo.Member.Name}({parameterInfo.Name})");

            var fileKey = GetKey(parameterInfo);
            if (!content.TryGetValue(fileKey, out MultipartContentTokenParser valueToBind))
                return onParsed(parameterInfo.ParameterType.GetDefault());

            return blobValueProvider.ProvideValue(valueToBind,
                boundValue => onParsed(boundValue),
                why => onParsed(why));

        }

        public override TResult ParseContentDelegate<TResult>(IFormCollection formData,
                ParameterInfo parameterInfo, 
                IApplication httpApp, IHttpRequest request,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            if (!parameterInfo.TryGetAttributeInterfaceFromChain(httpApp, out IProvideBlobValue blobValueProvider))
                return onFailure($"Could not identify an {nameof(IProvideBlobValue)} attribute on {parameterInfo.Member.DeclaringType.FullName}..{parameterInfo.Member.Name}({parameterInfo.Name})");

            var fileKey = GetKey(parameterInfo);
            return formData.Files
                .Where(fdf => String.Equals(fdf.Name, fileKey, StringComparison.OrdinalIgnoreCase))
                .First(
                    (fdf, next) =>
                    {

                        return blobValueProvider.ProvideValue(fdf,
                            boundValue => onParsed(boundValue),
                            why => onParsed(parameterInfo.ParameterType.GetDefault()));
                    },
                    () =>
                    {
                        return onParsed(parameterInfo.ParameterType.GetDefault());
                    });
        }

    }
}
