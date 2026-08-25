using EastFive.Api.Resources;
using EastFive.Api.Bindings;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class UpdateIdAttribute : QueryValidationAttribute, IDocumentParameter,
        IBindJsonApiValue, IBindMultipartApiValue, IBindFormDataApiValue,
        IProvideBindingRequirements, IModifyRoutePattern
    {
        public (IReadOnlyList<BindingRequirement> requirements, AssembleParameter assemble)
            GetParameterBinding(ParameterInfo parameter)
            => (new[] { GetRequirement(parameter) }, values => (values[0], null));

        /// <summary>
        /// <c>[UpdateId]</c> reads the resource id from the URL path
        /// (e.g. <c>PATCH /api/Resource/{id}</c>), the query string, or the
        /// body. Like <see cref="QueryIdAttribute"/>, it contributes the
        /// trailing path-segment capture so the <c>/Resource/{id}</c> route
        /// matches; without it the regex stops at <c>/Resource</c> and a
        /// path-style id produces no route match (404).
        /// </summary>
        public string ModifyRoutePattern(MethodInfo method, ParameterInfo parameter, string currentPattern)
            => RoutePattern.AppendTrailingCapture(currentPattern, this.GetKey(parameter),
                parameter.ParameterType);

        public BindingRequirement GetRequirement(ParameterInfo parameter)
        {
            return new BindingRequirement(
                    path: this.GetKey(parameter),
                    source: BindingSource.Anywhere,
                    parameter: parameter,
                    isOptional: false)
                .AddConverter<JContainer>((raw, param, app, req, onParsed, onFailure) =>
                    this.ParseContentDelegate<BindResult>(raw, contentString: null, bindConvert: null,
                        param, app, req, onParsed, onFailure))
                .AddConverter<IFormCollection>((raw, param, app, req, onParsed, onFailure) =>
                    this.ParseContentDelegate<BindResult>(raw, param, app, req, onParsed, onFailure))
                .AddConverter<string>((raw, param, app, req, onParsed, onFailure) =>
                    app.Bind(raw, param, onParsed, onFailure));
        }

        public override string Name
        {
            get
            {
                var name = base.Name;
                if (name.HasBlackSpace())
                    return name;
                return "id";
            }
            set => base.Name = value;
        }

        public Parameter GetParameter(ParameterInfo paramInfo, HttpApplication httpApp)
        {
            return new Parameter(paramInfo)
            {
                Default = true,
                Name = this.GetKey(paramInfo),
                Required = true,
                Type = Parameter.GetTypeName(paramInfo.ParameterType, httpApp),
                Where = "QUERY|BODY",
                OpenApiType = Parameter.GetOpenApiTypeName(paramInfo.ParameterType, httpApp),
            };
        }

        public TResult ParseContentDelegate<TResult>(JContainer contentJContainer,
                string contentString, Serialization.BindConvert bindConvert,
                ParameterInfo paramInfo,
                IApplication httpApp, IHttpRequest request,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            if (!(contentJContainer is JObject))
                return onFailure($"JSON Content is {contentJContainer.Type} and ID property for updating can only be parsed from objects.");
            var contentJObject = contentJContainer as JObject;

            var key = this.GetKey(paramInfo);
            return PropertyAttribute.ParseJsonContentDelegate(contentJObject,
                    contentString, bindConvert,
                    key, paramInfo,
                    httpApp, request,
                onParsed,
                onFailure);
        }

        public TResult ParseContentDelegate<TResult>(
                IDictionary<string, MultipartContentTokenParser> contentsLookup, 
                ParameterInfo parameterInfo, IApplication httpApp, IHttpRequest request,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            var key = this.GetKey(parameterInfo);
            if (!contentsLookup.ContainsKey(key))
                return onFailure("Key not found");

            var type = parameterInfo.ParameterType;
            return PropertyAttribute.ContentToType(httpApp, parameterInfo, contentsLookup[key],
                    onParsed,
                    onFailure);
        }

        public TResult ParseContentDelegate<TResult>(IFormCollection formData,
                ParameterInfo parameterInfo,
                IApplication httpApp, IHttpRequest request, 
            Func<object, TResult> onParsed, 
            Func<string, TResult> onFailure)
        {
            var key = this.GetKey(parameterInfo);
            return PropertyAttribute.ParseContentDelegate(key, formData,
                    parameterInfo, httpApp,
                onParsed,
                onFailure);
        }
    }
}
