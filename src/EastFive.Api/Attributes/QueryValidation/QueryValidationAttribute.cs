using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;

using Newtonsoft.Json.Linq;

using EastFive;
using EastFive.Extensions;
using EastFive.Api.Bindings;
using EastFive.Api.Core;
using EastFive.Api.Resources;
using EastFive.Api.Serialization;
using EastFive.Linq;
using EastFive.Reflection;
using EastFive.Serialization;

namespace EastFive.Api
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public class QueryValidationAttribute : System.Attribute, IBindApiValue
    {
        public virtual string Name { get; set; }

        public virtual SelectParameterResult TryCast(BindingData bindingData)
        {
            var parameterRequiringValidation = bindingData.parameterRequiringValidation;
            var key = GetKey(parameterRequiringValidation);
            var fileResult = bindingData.fetchDefaultParam(parameterRequiringValidation,
                (v) =>
                {
                    return SelectParameterResult.File(v, key, parameterRequiringValidation);
                },
                (whyQuery) => SelectParameterResult.FailureFile(
                    $"Could not create value in file.",
                    key, parameterRequiringValidation));

            if (fileResult.valid)
                return fileResult;

            var queryResult = bindingData.fetchQueryParam(parameterRequiringValidation,
                (v) =>
                {
                    if (fileResult.valid)
                    {
                        fileResult.fromQuery = true;
                        return fileResult;
                    }
                    return SelectParameterResult.Query(v, key, parameterRequiringValidation);
                },
                (whyQuery) =>
                {
                    if (fileResult.valid)
                        return fileResult;
                    return SelectParameterResult.FailureQuery(whyQuery, key, parameterRequiringValidation);
                });

            if (queryResult.valid)
                return queryResult;

            return bindingData.fetchBodyParam(parameterRequiringValidation,
                (v) =>
                {
                    return SelectParameterResult.Body(v, key, parameterRequiringValidation);
                },
                (whyQuery) =>
                {
                    if (queryResult.valid)
                        return queryResult;
                    return SelectParameterResult.FailureBody(whyQuery, key, parameterRequiringValidation);
                });
        }

        public virtual string GetKey(ParameterInfo paramInfo)
        {
            if (this.Name.HasBlackSpace())
                return this.Name;
            return paramInfo.Name;
        }

    }

    public class QueryParameterAttribute : QueryValidationAttribute, IDocumentParameter, IBindQueryApiValue,
        IProvideBindingRequirements, IModifyRoutePattern
    {
        public bool CheckFileName { get; set; }

        public override SelectParameterResult TryCast(BindingData bindingData)
        {
            var parameterRequiringValidation = bindingData.parameterRequiringValidation;
            var key = this.GetKey(parameterRequiringValidation);
            return TryCast(bindingData, key, this.CheckFileName);
        }

        public virtual (IReadOnlyList<BindingRequirement> requirements, AssembleParameter assemble)
            GetParameterBinding(ParameterInfo parameter)
            => (new[] { GetRequirement(parameter) }, values => (values[0], null));

        /// <summary>
        /// Default mutation: contribute a trailing capture only when this
        /// attribute participates in the URL path
        /// (<see cref="CheckFileName"/> = true). <see cref="QueryIdAttribute"/>
        /// overrides this to always contribute.
        /// </summary>
        public virtual string ModifyRoutePattern(MethodInfo method, ParameterInfo parameter, string currentPattern)
        {
            if (!this.CheckFileName)
                return currentPattern;
            return RoutePattern.AppendTrailingCapture(currentPattern, this.GetKey(parameter),
                parameter.ParameterType);
        }

        public virtual BindingRequirement GetRequirement(ParameterInfo parameter)
        {
            return new BindingRequirement(
                    path: this.GetKey(parameter),
                    source: this.CheckFileName
                        ? (BindingSource.Query | BindingSource.Path)
                        : BindingSource.Query,
                    parameter: parameter,
                    isOptional: false)
                .AddConverter<string>((raw, param, app, req, onParsed, onFailure) =>
                    app.Bind(raw, param, onParsed, onFailure));
        }

        public override string GetKey(ParameterInfo paramInfo)
        {
            return base.GetKey(paramInfo).ToLower();
        }

        public virtual Parameter GetParameter(ParameterInfo paramInfo, HttpApplication httpApp)
        {
            return new Parameter(paramInfo)
            {
                Default = CheckFileName,
                Name = this.GetKey(paramInfo),
                Required = true,
                Type = Parameter.GetTypeName(paramInfo.ParameterType, httpApp),
                Where = "QUERY",
                OpenApiType = Parameter.GetOpenApiTypeName(paramInfo.ParameterType, httpApp),
            };
        }

        public static SelectParameterResult TryCast(BindingData bindingData,
            string key, bool checkFileName)
        {
            var parameterRequiringValidation = bindingData.parameterRequiringValidation;
            var queryResult = bindingData.fetchQueryParam(parameterRequiringValidation,
                (v) =>
                {
                    return SelectParameterResult.Query(v, key, parameterRequiringValidation);
                },
                (whyQuery) => SelectParameterResult.FailureQuery(whyQuery, key, parameterRequiringValidation));

            if (queryResult.valid)
                return queryResult;

            if (!checkFileName)
                return queryResult;

            return bindingData.fetchDefaultParam(parameterRequiringValidation,
                (v) =>
                {
                    return SelectParameterResult.File(v, key, parameterRequiringValidation);
                },
                (whyQuery) =>
                {
                    return queryResult;
                });
        }

        // TODO: This is the approach we'll be moving to
        public TResult ParseContentDelegate<TResult>(IDictionary<string, string> pairs,
                ParameterInfo parameterInfo, IApplication httpApp, IHttpRequest routeData,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            throw new NotImplementedException();
        }

        //public TResult ParseContentDelegate<TResult>(JObject contentJObject, string contentString, BindConvert bindConvert, ParameterInfo parameterInfo, IApplication httpApp, HttpRequestMessage request, Func<object, TResult> onParsed, Func<string, TResult> onFailure)
        //{
        //    throw new NotImplementedException();
        //}
    }

    public class OptionalQueryParameterAttribute : QueryParameterAttribute
    {
        public override BindingRequirement GetRequirement(ParameterInfo parameter)
        {
            return new BindingRequirement(
                    path: this.GetKey(parameter),
                    source: this.CheckFileName
                        ? (BindingSource.Query | BindingSource.Path)
                        : BindingSource.Query,
                    parameter: parameter,
                    isOptional: true,
                    optionalDefault: p => p.ParameterType.IsSubClassOfGeneric(typeof(IRefOptional<>))
                        ? RefOptionalHelper.CreateEmpty(p.ParameterType.GenericTypeArguments.First())
                        : p.ParameterType.GetDefault())
                .AddConverter<string>((raw, param, app, req, onParsed, onFailure) =>
                    app.Bind(raw, param, onParsed, onFailure));
        }

        public override SelectParameterResult TryCast(BindingData bindingData)
        {
            var parameterRequiringValidation = bindingData.parameterRequiringValidation;
            var baseValue = base.TryCast(bindingData);
            if (baseValue.valid)
                return baseValue;

            baseValue.value = GetValue();
            baseValue.valid = true;
            baseValue.fromQuery = true;
            return baseValue;

            object GetValue()
            {
                if (parameterRequiringValidation.ParameterType.IsSubClassOfGeneric(typeof(IRefOptional<>)))
                {
                    var instance = RefOptionalHelper.CreateEmpty(
                        parameterRequiringValidation.ParameterType.GenericTypeArguments.First());
                    return instance;
                }

                return parameterRequiringValidation.ParameterType.GetDefault();
            }
        }

        public override Parameter GetParameter(ParameterInfo paramInfo, HttpApplication httpApp)
        {
            var parameter = base.GetParameter(paramInfo, httpApp);
            parameter.Required = false;
            return parameter;
        }
    }

    public class QueryIdAttribute : QueryParameterAttribute
    {
        public override BindingRequirement GetRequirement(ParameterInfo parameter)
        {
            return new BindingRequirement(
                    path: this.GetKey(parameter),
                    source: BindingSource.Query | BindingSource.Path,
                    parameter: parameter,
                    isOptional: false)
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

        public override SelectParameterResult TryCast(BindingData bindingData)
        {
            base.CheckFileName = true;
            return base.TryCast(bindingData);
        }

        /// <summary>
        /// <c>[QueryId]</c> always lives in the trailing path segment
        /// (e.g. <c>/api/Resource/{id}</c>) regardless of
        /// <see cref="QueryParameterAttribute.CheckFileName"/>.
        /// </summary>
        public override string ModifyRoutePattern(MethodInfo method, ParameterInfo parameter, string currentPattern)
            => RoutePattern.AppendTrailingCapture(currentPattern, this.GetKey(parameter),
                parameter.ParameterType);

        public override Parameter GetParameter(ParameterInfo paramInfo, HttpApplication httpApp)
        {
            var parameter = base.GetParameter(paramInfo, httpApp);
            parameter.Default = true;
            return parameter;
        }
    }

    public class HashedFileAttribute : QueryValidationAttribute, IDocumentParameter,
        IProvideBindingRequirements
    {
        public override SelectParameterResult TryCast(BindingData bindingData)
        {
            var parameterRequiringValidation = bindingData.parameterRequiringValidation;
            var key = this.GetKey(parameterRequiringValidation);
            return bindingData.request.GetAbsoluteUri()
                .VerifyParametersHash(
                    onValid: (id, paramsHash) =>
                     {
                         var resourceType = parameterRequiringValidation
                             .ParameterType.GenericTypeArguments.First();
                         var instantiatableType = typeof(CheckSumRef<>).MakeGenericType(resourceType);
                         var instance = Activator.CreateInstance(instantiatableType, new object[] { id, paramsHash });
                         return SelectParameterResult.File(instance, key, parameterRequiringValidation);
                     },
                    onInvalid: (why) => SelectParameterResult
                         .FailureFile(why, key, parameterRequiringValidation));
        }

        public (IReadOnlyList<BindingRequirement> requirements, AssembleParameter assemble)
            GetParameterBinding(ParameterInfo parameter)
            => (new[] { GetRequirement(parameter) }, values => (values[0], null));

        public BindingRequirement GetRequirement(ParameterInfo parameter)
        {
            return new BindingRequirement(
                    path: this.GetKey(parameter),
                    source: BindingSource.Request,
                    parameter: parameter,
                    isOptional: false)
                .AddConverter<string>((raw, param, app, req, onParsed, onFailure) =>
                    req.GetAbsoluteUri().VerifyParametersHash(
                        onValid: (id, paramsHash) =>
                        {
                            var resourceType = param.ParameterType.GenericTypeArguments.First();
                            var instantiatableType = typeof(CheckSumRef<>).MakeGenericType(resourceType);
                            var instance = Activator.CreateInstance(instantiatableType, new object[] { id, paramsHash });
                            return onParsed(instance);
                        },
                        onInvalid: why => onFailure(why)));
        }

        public virtual Parameter GetParameter(ParameterInfo paramInfo, HttpApplication httpApp)
        {
            return new Parameter(paramInfo)
            {
                Default = true,
                Name = this.GetKey(paramInfo),
                Required = true,
                Type = $"Hashed({Parameter.GetTypeName(paramInfo.ParameterType.GenericTypeArguments.First(), httpApp)})",
                Where = "QUERY",
                OpenApiType = Parameter.GetOpenApiTypeName(paramInfo.ParameterType, httpApp),
            };
        }
    }

    public class AcceptsAttribute : Attribute, IBindApiValue, IProvideBindingRequirements
    {
        public string Media { get; set; }

        public string GetKey(ParameterInfo paramInfo)
        {
            return "__accept-header__";
        }

        public (IReadOnlyList<BindingRequirement> requirements, AssembleParameter assemble)
            GetParameterBinding(ParameterInfo parameter)
            => (new[] { GetRequirement(parameter) }, values => (values[0], null));

        public BindingRequirement GetRequirement(ParameterInfo parameter)
        {
            var media = this.Media;
            return new BindingRequirement(
                    path: this.GetKey(parameter),
                    source: BindingSource.Request,
                    parameter: parameter,
                    isOptional: false)
                .AddConverter<string>((raw, param, app, req, onParsed, onFailure) =>
                {
                    if (param.ParameterType != typeof(MediaTypeWithQualityHeaderValue))
                        return onFailure(
                            $"No accept binding for type `{param.ParameterType.FullName}` (try MediaTypeWithQualityHeaderValue).");
                    var match = req.GetAcceptTypes()
                        .FirstOrDefault(accept =>
                            accept.MediaType != null
                            && accept.MediaType.ToLower().Contains(media ?? string.Empty));
                    return onParsed(match);
                });
        }

        public SelectParameterResult TryCast(BindingData bindingData)
        {
            var request = bindingData.request;
            var parameterRequiringValidation = bindingData.parameterRequiringValidation;
            var bindType = parameterRequiringValidation.ParameterType;
            if (typeof(MediaTypeWithQualityHeaderValue) == bindType)
            {
                return request.GetAcceptTypes()
                    .Where(accept => accept.MediaType.ToLower().Contains(this.Media))
                    .First(
                        (accept, next) =>
                        {
                            return new SelectParameterResult
                            {
                                valid = true,
                                fromBody = false,
                                fromQuery = false,
                                fromFile = false,
                                key = GetKey(parameterRequiringValidation),
                                parameterInfo = parameterRequiringValidation,
                                value = accept,
                            };
                        },
                        () =>
                        {
                            return new SelectParameterResult
                            {
                                valid = true,
                                fromBody = false,
                                fromQuery = false,
                                fromFile = false,
                                key = GetKey(parameterRequiringValidation),
                                parameterInfo = parameterRequiringValidation,
                                value = default(MediaTypeWithQualityHeaderValue),
                            };
                        });
            }
            return new SelectParameterResult
            {
                key = "",
                fromQuery = false,
                fromFile = false,
                fromBody = false,
                parameterInfo = parameterRequiringValidation,
                valid = false,
                failure = $"No accept binding for type `{bindType.FullName}` (try MediaTypeWithQualityHeaderValue).",
            };
        }
    }

    public class HeaderLogAttribute : QueryValidationAttribute
    {
        public override SelectParameterResult TryCast(BindingData bindingData)
        {
            var parameterRequiringValidation = bindingData.parameterRequiringValidation;
            var logger = bindingData.httpApp.Logger;
            return new SelectParameterResult
            {
                value = logger,
                key = "",
                fromBody = false,
                fromQuery = false,
                fromFile = false,
                parameterInfo = parameterRequiringValidation,
                valid = true,
            };
        }
    }

    
}
