using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using EastFive.Api.Core;
using EastFive.Api.Serialization;
using EastFive.Linq;
using EastFive.Extensions;
using EastFive.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace EastFive.Api
{
    public interface IHttpHeaderValueCollection<THeaderValue>
    {
        CultureInfo[] ToCultures();
    }

    public class HeaderAttribute : QueryValidationAttribute, IBindJsonApiValue, IBindFormDataApiValue,
        IProvideBindingRequirements
    {
        public string Content { get; set; }

        public override string GetKey(ParameterInfo paramInfo)
        {
            if (this.Content.HasBlackSpace())
                return this.Content;
            return base.GetKey(paramInfo);
        }

        public (IReadOnlyList<BindingRequirement> requirements, AssembleParameter assemble)
            GetParameterBinding(ParameterInfo parameter)
            => (new[] { GetRequirement(parameter) }, values => (values[0], null));

        // ---- BindingRequirement ------------------------------------------------
        // Two shapes are observed:
        //   (a) Content blank → MediaTypeHeaderValue from the request media type
        //       (Source = Request).
        //   (b) Content set + body → MediaTypeHeaderValue parsed out of a JSON
        //       data: URL (Source = Body, JContainer converter).
        // The requirement registers both sources; whichever the envelope/request
        // satisfies first wins, with Request preferred (it's the cheaper read).
        public BindingRequirement GetRequirement(ParameterInfo parameter)
        {
            var key = this.GetKey(parameter);
            var source = this.Content.HasBlackSpace()
                ? (BindingSource.Body | BindingSource.Request)
                : BindingSource.Request;
            return new BindingRequirement(
                    path: key,
                    source: source,
                    parameter: parameter,
                    isOptional: false)
                .AddConverter<string>((raw, param, app, req, onParsed, onFailure) =>
                {
                    if (param.ParameterType == typeof(MediaTypeHeaderValue))
                        return onParsed(req.GetMediaType());
                    return onFailure($"Header attribute on type {param.ParameterType.FullName} requires Content to be set.");
                })
                .AddConverter<JContainer>((raw, param, app, req, onParsed, onFailure) =>
                    this.ParseContentDelegate<BindResult>(raw, contentString: null, bindConvert: null,
                        param, app, req, onParsed, onFailure));
        }

        public TResult ParseContentDelegate<TResult>(JContainer contentJContainer, string contentString, 
                BindConvert bindConvert, 
                ParameterInfo parameterInfo, 
                IApplication httpApp, IHttpRequest request, 
            Func<object, TResult> onParsed, 
            Func<string, TResult> onFailure)
        {
            if (!(contentJContainer is JObject))
                return onFailure($"JSON Content is {contentJContainer.Type} and headers can only be parsed from objects.");
            var contentJObject = contentJContainer as JObject;

            var key = Content;
            if (!contentJObject.TryGetValue(key, out JToken valueToken))
                return onFailure($"Key[{key}] was not found in JSON");

            if (typeof(System.Net.Http.Headers.MediaTypeHeaderValue) == parameterInfo.ParameterType)
            {
                var contentEncodedBase64String = valueToken.Value<string>();
                var mediaHeaderType = contentEncodedBase64String.MatchRegexInvoke(
                    "data:(?<contentType>[^;]+);base64,.+",
                    (contentType) => new MediaTypeHeaderValue(contentType),
                    types => types.First(
                        (ct, next) => ct,
                        () => new MediaTypeHeaderValue("application/data")));
                return onParsed(mediaHeaderType);
            }

            throw new NotImplementedException();
        }

        public override SelectParameterResult TryCast(BindingData bindingData)
        {
            var request = bindingData.request;
            var parameterRequiringValidation = bindingData.parameterRequiringValidation;
            var bindType = parameterRequiringValidation.ParameterType;
            request.GetAbsoluteUri();
            if (typeof(MediaTypeHeaderValue) == bindType)
            {
                if (Content.IsNullOrWhiteSpace())
                    return new SelectParameterResult
                    {
                        valid = true,
                        fromBody = false,
                        fromQuery = false,
                        fromFile = false,
                        key = Content,
                        parameterInfo = parameterRequiringValidation,
                        value = request.GetMediaType(),
                    };

                return bindingData.fetchBodyParam(
                    parameterRequiringValidation,
                    (value) =>
                    {
                        return new SelectParameterResult
                        {
                            valid = true,
                            fromBody = false,
                            fromQuery = false,
                            fromFile = false,
                            key = Content,
                            parameterInfo = parameterRequiringValidation,
                            value = value,
                        };
                    },
                    (why) =>
                    {
                        return new SelectParameterResult
                        {
                            fromBody = false,
                            key = "",
                            fromQuery = false,
                            fromFile = false,
                            parameterInfo = parameterRequiringValidation,
                            valid = false,
                            failure = why, // $"Cannot extract MediaTypeHeaderValue from non-multipart request.",
                        };
                    });
            }
            if (bindType.IsSubClassOfGeneric(typeof(IHttpHeaderValueCollection<>)))
            {
                if (bindType.GenericTypeArguments.First() == typeof(StringWithQualityHeaderValue))
                {
                    if (Content.HasBlackSpace())
                        return new SelectParameterResult
                        {
                            key = "",
                            fromQuery = false,
                            fromBody = false,
                            fromFile = false,
                            parameterInfo = parameterRequiringValidation,
                            valid = false,
                            failure = "AcceptLanguage is not a content header.",
                        };
                    var accepts = request.GetAcceptLanguage().ToArray();
                    var valueCast = new HeaderValues<StringWithQualityHeaderValue>(accepts);
                    return new SelectParameterResult
                    {
                        valid = true,
                        fromBody = false,
                        fromQuery = false,
                        fromFile = false,
                        key = default,
                        parameterInfo = parameterRequiringValidation,
                        value = valueCast,
                    };
                }
            }
            return new SelectParameterResult
            {
                fromBody = false,
                key = "",
                fromQuery = false,
                parameterInfo = parameterRequiringValidation,
                valid = false,
                failure = $"No header binding for type `{bindType.FullName}`.",
            };
        }

        public TResult ParseContentDelegate<TResult>(IFormCollection formData,
                ParameterInfo parameterInfo, IApplication httpApp, IHttpRequest routeData,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            var key = this.GetKey(parameterInfo);
            return ParseContentDelegate<TResult>(key, formData,
                    parameterInfo, httpApp, this.GetType(),
                onParsed,
                onFailure);
        }

        public static TResult ParseContentDelegate<TResult>(string key, IFormCollection formData,
                ParameterInfo parameterInfo, IApplication httpApp, Type thisType,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            var type = parameterInfo.ParameterType;

            if (formData.IsDefaultOrNull())
                return onFailure("No form data provided");
            
            return formData.Files
                .Where(file => file.Name == key)
                .First(
                    (file, next) =>
                    {
                        if (type.IsAssignableFrom(typeof(MediaTypeHeaderValue)))
                        {
                            var mediaType = new MediaTypeHeaderValue(file.ContentType);
                            return onParsed(mediaType);
                        };
                        return onFailure($"{thisType.FullName} does not bind to type {type.FullName}");
                    },
                    () => onFailure("File not found"));
        }

        private class HeaderValues<T> : IHttpHeaderValueCollection<T>
        {
            private StringWithQualityHeaderValue[] accepts;

            public HeaderValues(StringWithQualityHeaderValue[] accepts)
            {
                this.accepts = accepts;
            }

            public CultureInfo[] ToCultures()
            {
                var acceptLookup = accepts
                    .NullToEmpty()
                    .Select(acceptHeader => acceptHeader.Value.ToLowerInvariant().PairWithValue(acceptHeader.Quality))
                    .ToDictionary();
                return CultureInfo.GetCultures(CultureTypes.AllCultures)
                    .OrderBy(
                        culture =>
                        {
                            var lookupKey = culture.Name.ToLowerInvariant();
                            if (!acceptLookup.ContainsKey(lookupKey))
                                return -1.0;
                            var valueMaybe = acceptLookup[lookupKey];
                            if (!valueMaybe.HasValue)
                                return -1.0;
                            return valueMaybe.Value;
                        })
                    .ToArray();
            }
        }
    }

    public class HeaderOptionalAttribute : HeaderAttribute
    {
        public override SelectParameterResult TryCast(BindingData bindingData)
        {
            var parameterRequiringValidation = bindingData.parameterRequiringValidation;
            var baseValue = base.TryCast(bindingData);
            if (baseValue.valid)
                return baseValue;

            baseValue.valid = true;
            baseValue.fromBody = true;
            baseValue.value = parameterRequiringValidation.ParameterType.GetDefault();
            return baseValue;
        }
    }
}
