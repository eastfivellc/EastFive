using EastFive.Api.Meta.Postman.Resources.Collection;
using EastFive.Api.Resources;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Serialization;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Meta.Flows
{
    public interface IDefineFlow
    {
        public string FlowName { get; }

        public string Version { get; }

        public double Step { get; }

        string GetScope();

        Item GetItem(Api.Resources.Method method, bool preferJson);
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class WorkflowStepAttribute : System.Attribute, IDefineFlow
    {
        public string FlowName { get; set; }

        public string Version { get; set; }

        public string StepName { get; set; }

        public double Step { get; set; }

        public string Scope { get; set; }

        public bool FollowRedirects { get; set; } = true;

        public virtual Item GetItem(Method method, bool preferJson)
        {
            var item = new Item
            {
                name = GetStepName(method),
                request = GetRequest(method, preferJson),
                _event = new Event []
                {
                    new Event()
                    {
                        listen = "test",
                        script = new Script()
                        {
                            type = "text/javascript",
                            exec = method.HasValue(
                                (v) => GetScriptSteps(v),
                                () => default(string[])),
                        },
                    }
                },
            };
            if(!FollowRedirects)
            {
                item.protocolProfileBehavior = new ProtocolProfileBehavior
                {
                    followRedirects = FollowRedirects
                };
            }
            return item;
        }

        public virtual string GetScope()
        {
            return Scope.HasBlackSpace() ? Scope : string.Empty;
        }

        private string [] GetScriptSteps(Method method)
        {
            if (method.IsDefaultOrNull())
                return new string[] { };

            var methodSteps = GetSteps(method.AsArray(),
                method => method.MethodPoco.GetAttributesInterface<IDefineWorkflowScriptMethod>(),
                (method, attr) => attr.GetScriptLines(method));

            var parameterSteps = GetSteps(method.Parameters,
                param => param.PocoParameter
                    .GetAttributesInterface<IDefineWorkflowScriptParam>(),
                (param, attr) => attr.GetScriptLines(param, method));

            var responseSteps = method.Responses
                .SelectMany(item => item.ParamInfo
                    .GetAttributesInterface<IDefineWorkflowScriptResponse>()
                    .Select(attr => (item, attr)))
                .SelectMany(
                    tpl =>
                    {
                        var (response, attr) = tpl;
                        var initLines = attr.GetInitializationLines(response, method);
                        var scriptLines = attr.GetScriptLines(response, method);
                        return initLines.Concat(scriptLines).IfMatchesResponse(response);
                    })
                .ToArray();

            //var responseSteps = GetSteps(method.Responses,
            //        response => response.ParamInfo
            //            .GetAttributesInterface<IDefineWorkflowScriptResponse>(),
            //        (response, attr) => attr
            //            .GetScriptLines(response, method));

            return methodSteps
                .Concat(parameterSteps)
                .Concat(responseSteps)
                .ToArray();


            string [] GetSteps<T, TAttr>(IEnumerable<T> items,
                Func<T, TAttr[]> getAttrs,
                Func<T, TAttr, string []> getLines)
                where TAttr : IDefineWorkflowScript<T>
            {
                var itemAttrs = items
                    .SelectMany(item => getAttrs(item)
                        .Select(attr => (item, attr)))
                    .ToArray();

                var initLines = itemAttrs
                    .SelectMany(tpl => tpl.attr.GetInitializationLines(tpl.item, method))
                    .Distinct(line => line.MD5HashGuid())
                    .ToArray();

                var assignmentLines = itemAttrs
                    .SelectMany(tpl => getLines(tpl.item, tpl.attr))
                    .ToArray();

                var completeLines = initLines
                    .Concat(assignmentLines)
                    .ToArray();

                return completeLines;
            }

            //var queryParamTestLines = new string[] { };
            //var queryParamLineParams = method.MethodPoco
            //    .GetParametersAndAttributesInterface<IDefineWorkflowRequestVariable>()
            //    .Where(paramTpl => !paramTpl.Item1.ContainsAttributeInterface<IBindQueryApiValue>())
            //    .ToArray();
            //if (queryParamLineParams.Any())
            //{
            //    var lineVarQuery = "var query = {};\r";
            //    var linePopulateQuery = "pm.request.url.query.all().forEach((param) => { query[param.key] = param.value});\r";
            //    var linesQueryVariables = queryParamLineParams
            //        .SelectMany(
            //            tpl =>
            //            {
            //                var (parameterInfo, attr) = tpl;
            //                var (name, value) = attr.GetNameAndValue(parameterInfo, method);
            //                var lineExtract = $"var queryParam_{name} = query[\"{value}\"];\r";
            //                var lineMakeGlobal = $"pm.environment.set(\"{name}\", queryParam_{name});\r";
            //                return new string[] { lineExtract, lineMakeGlobal, "\r" };
            //            })
            //        .ToArray();
            //    queryParamTestLines = linesQueryVariables
            //        .Prepend(linePopulateQuery)
            //        .Prepend(lineVarQuery)
            //        .ToArray();
            //}

            //var requestJsonPropertyLineParams = method.MethodPoco
            //    .GetParametersAndAttributesInterface<IDefineWorkflowRequestVariable>()
            //    .Where(paramTpl => paramTpl.Item1.ContainsAttributeInterface<IBindJsonApiValue>())
            //    .ToArray();
            //if (requestJsonPropertyLineParams.Any())
            //{
            //    var lineVarQuery = "let requestResource = JSON.parse(pm.request.body.raw);\r";
            //    var linesJsonPropertyVariables = requestJsonPropertyLineParams
            //        .Select(
            //            tpl =>
            //            {
            //                var (parameterInfo, attr) = tpl;
            //                var (name, value) = attr.GetNameAndValue(parameterInfo, method);
            //                var lineMakeGlobal = $"pm.environment.set(\"{name}\", requestResource.{value});\r";
            //                return lineMakeGlobal;
            //            })
            //        .ToArray();
            //    queryParamTestLines = queryParamTestLines
            //        .Append(lineVarQuery)
            //        .Concat(linesJsonPropertyVariables)
            //        .ToArray();
            //}

            //var headerParamTestLines = new string[] { };
            //var headerLineParams = method.Responses
            //    .TryWhere((Resources.Response response, out IDefineWorkflowVariableFromHeader workflowResponse) =>
            //        response.ParamInfo.TryGetAttributeInterface(out workflowResponse, inherit: true))
            //    .ToArray();
            //if(headerLineParams.Any())
            //{
            //    var lineVarHeaders = "var headers = {};\r";
            //    var linePopulateHeaders = "pm.response.headers.all().forEach((header) => { headers[header.key] = header.value });\r";
            //    var linesQueryHeaders = headerLineParams
            //        .SelectMany(
            //            itemAttrTpl =>
            //            {
            //                var (responseParameter, attr) = itemAttrTpl;
            //                var (name, value) = attr.GetNameAndValue(responseParameter, method);
            //                var lineExtract = $"var headerParam_{name} = headers[\"{value}\"];\r";
            //                var lineMakeGlobal = $"pm.environment.set(\"{name}\", headerParam_{name});\r";
            //                return new string[] { lineExtract, lineMakeGlobal, "\r" };
            //            })
            //        .ToArray();
            //    headerParamTestLines = linesQueryHeaders
            //        .Prepend(linePopulateHeaders)
            //        .Prepend(lineVarHeaders)
            //        .ToArray();
            //}

            //return queryParamTestLines
            //    .Concat(headerParamTestLines)
            //    .Concat(paramTypeTestLines).ToArray();
        }

        private string GetStepName(Method method)
        {
            if (StepName.HasBlackSpace())
                return StepName;

            if (method.HttpMethod.Equals(HttpMethod.Post.Method, System.StringComparison.OrdinalIgnoreCase))
                return $"Create {method.Route.Name}";

            if (method.HttpMethod.Equals(HttpMethod.Put.Method, System.StringComparison.OrdinalIgnoreCase))
                return $"Replace {method.Route.Name}";

            if (method.HttpMethod.Equals(HttpMethod.Delete.Method, StringComparison.OrdinalIgnoreCase))
                return $"Delete {method.Route.Name}";

            if (method.HttpMethod.Equals(HttpMethod.Get.Method, StringComparison.OrdinalIgnoreCase))
            {
                if(method.Responses.Any(response => response.IsMultipart))
                    return $"List {method.Route.Name}";
                return $"Get {method.Route.Name}";
            }

            if (method.HttpMethod.Equals(HttpMethod.Patch.Method, StringComparison.OrdinalIgnoreCase))
                return $"Update {method.Route.Name}";
            
            return method.Route.Name;
        }

        protected virtual Request GetRequest(Method method, bool preferJson)
        {
            var bodyProperties = method.MethodPoco
                .GetParameters()
                .SelectMany(paramInfo => paramInfo
                    .GetAttributesInterface<IDefineWorkflowRequestProperty>()
                    .Select(requestProperty => (item: paramInfo, @out: requestProperty)))
                .ToArray();

            var body = bodyProperties.IsDefaultNullOrEmpty() ?
                default(Body)
                :
                preferJson?
                    PopulateJsonBody()
                    :
                    PopulateBody();

            var paramQueryItems = method.MethodPoco
                .GetParameters()
                .TryWhere((ParameterInfo paramInfo, out IDefineQueryItem requestProperty) =>
                    paramInfo.TryGetAttributeInterface(out requestProperty))
                .SelectMany(tpl => tpl.@out.GetQueryItem(method, tpl.item).NullToEmpty())
                .ToArray();
            var attrQueryItems = method
                .MethodPoco
                .CustomAttributes
                .SelectMany(
                    attr =>
                    {
                        return attr.AttributeType.GetAttributesInterface<IDefineQueryItem>();
                    })
                .SelectMany(attr => attr.GetQueryItems(method))
                .ToArray();

            var headersFromParameters = method.MethodPoco
                .GetParameters()
                .TryWhere(
                    (ParameterInfo paramInfo, out IDefineHeader headerDefinition) =>
                    {
                        if (paramInfo.TryGetAttributeInterface(out headerDefinition))
                            return true;

                        // See if the parameter type has a header requirement
                        if (paramInfo.ParameterType.TryGetAttributeInterface(out headerDefinition))
                            return true;
                        return false;
                    })
                .Select(tpl => tpl.@out.GetHeader(method, tpl.item))
                .ToArray();
            var headersFromAttributes = method
                .MethodPoco
                .GetAttributesInterface<IDefineHeader>()
                .Select(hdr => hdr.GetHeader(method, default))
                .ToArray();

            var queryItems = paramQueryItems
                .Concat(attrQueryItems)
                .Distinct(x => x.key)
                .ToArray();
            var headerItems = headersFromParameters
                .Concat(headersFromAttributes)
                .Distinct(x => x.key)
                .ToArray();

            var url = method.MethodPoco.TryGetAttributeInterface(out IProvideWorkflowUrl workflowUrlProvider) ?
                    workflowUrlProvider.GetUrl(method, queryItems)
                :
                    new Url()
                    {
                        raw = $"{{{{HOST}}}}/{method.Route.Namespace}/{method.Route.Name}",
                        host = Url.VariableHostName.AsArray(),
                        path = new string[] { method.Route.Namespace, method.Route.Name },
                        query = queryItems,
                    };

            return new Request()
            {
                method = method.HttpMethod,
                header = headerItems,
                body = body,
                url = url,
                description = GetDescription(),
            };


            Body PopulateBody()
            {
                var formdata = FormDataBody();

                if (!IsFormDataCapable())
                    return PopulateJsonBody();

                var rawJsonBody = RawJsonBody();

                var mode = IsFormDataCapable() ?
                    "formdata"
                    :
                    "raw";

                return new Body()
                {
                    mode = mode,
                    options = new Options()
                    {
                        raw = new Raw()
                        {
                            language = "json",
                        }
                    },
                    formdata = formdata,
                    raw = rawJsonBody,
                };

                bool IsFormDataCapable() => formdata.Length == bodyProperties.Length;

                FormData[] FormDataBody()
                {
                    return bodyProperties
                        .Where(
                            tpl => typeof(IDefineWorkflowRequestPropertyFormData)
                                .IsAssignableFrom(tpl.@out.GetType()))
                        .Select(tpl => ((IDefineWorkflowRequestPropertyFormData)tpl.@out, tpl.item))
                        .SelectMany(attr => attr.Item1.GetFormData(attr.item))
                        .ToArray();
                }

                string RawJsonBody()
                {
                    var sb = new StringBuilder();
                    using (var sw = new StringWriter(sb))
                    {
                        var jsonSerializer = new JsonSerializer();
                        using (var writer = new JsonTextWriter(sw)
                        {
                            Formatting = Formatting.Indented,
                        })
                        {
                            writer.WriteStartObject();
                            foreach (var requestProperty in bodyProperties)
                            {
                                requestProperty.@out.AddProperties(writer, requestProperty.item);
                            }
                            writer.WriteEndObject();
                        }
                        return sb.ToString();
                    }
                }

                //bool IsBodyEmpty()
                //{
                //    if (!formdata.AnyNullSafe())
                //        return false;

                //    if (rawJsonBody.Trim(new char[] {'{','}'}).HasBlackSpace())
                //        return false;

                //    return true;
                //}
            } 

            Body PopulateJsonBody()
            {
                var rawJsonBody = RawJsonBody();
                if (IsBodyEmpty())
                    return default(Body);

                return new Body()
                {
                    mode = "raw",
                    options = new Options()
                    {
                        raw = new Raw()
                        {
                            language = "json",
                        }
                    },
                    raw = rawJsonBody,
                };

                string RawJsonBody()
                {
                    var sb = new StringBuilder();
                    using (var sw = new StringWriter(sb))
                    {
                        var jsonSerializer = new JsonSerializer();
                        using (var writer = new JsonTextWriter(sw)
                        {
                            Formatting = Formatting.Indented,
                        })
                        {
                            writer.WriteStartObject();
                            foreach (var requestProperty in bodyProperties)
                            {
                                requestProperty.@out.AddProperties(writer, requestProperty.item);
                            }
                            writer.WriteEndObject();
                        }
                        return sb.ToString();
                    }
                }

                bool IsBodyEmpty()
                {
                    if (rawJsonBody.Trim(new char[] { '{', '}' }).HasBlackSpace())
                        return false;

                    return true;
                }
            }

            string GetDescription()
            {
                return method.Description;
            }
        }
    }

    public class WorkflowStep2Attribute : WorkflowStepAttribute
    {

    }

    public class WorkflowStep3Attribute : WorkflowStepAttribute
    {

    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class WorkflowStepCustomAttribute : WorkflowStepAttribute
    {
        public string HttpMethod { get; set; }

        public string[] HeaderKeys { get; set; }

        public string[] HeaderValues { get; set; }

        public string BodyRaw { get; set; }

        public bool IsBodyFile { get; set; }

        public string Url { get; set; }

        public string Description { get; set; }

        protected override Request GetRequest(Method method, bool preferJson)
        {
            bool didParseUrl = Uri.TryCreate(this.Url, UriKind.RelativeOrAbsolute, out Uri parsedUri);
            return new Request()
            {
                method = this.HttpMethod,
                header = this.HeaderKeys
                    .CollateSimple(this.HeaderValues)
                    .Select(tpl => new Header() { key = tpl.Item1, value = tpl.Item2  })
                    .ToArray(),
                body = this.BodyRaw.HasBlackSpace()?
                    new Body
                    {
                        mode = "raw",
                        raw = this.BodyRaw,
                    }
                    :
                    this.IsBodyFile?
                        new Body {
                            mode = "file",
                            file = new object() { },
                        }
                        :
                        default(Body),
                url = //didParseUrl?
                    //parsedUri.IsAbsoluteUri?
                    //    new Url()
                    //    {
                    //        raw = $"{{{{HOST}}}}/{this.Url}",
                    //        host = "{{HOST}}".AsArray(),
                    //        path = parsedUri.PathAndQuery
                    //            .Split('/')
                    //            .Where(item => !item.Contains('?'))
                    //            .ToArray(),
                    //        query = parsedUri
                    //            .ParseQuery()
                    //            .Select(kvp => new QueryItem() { key = kvp.Key, value = kvp.Value })
                    //            .ToArray(),
                    //    }
                    //    :
                    //    new Url()
                    //    {
                    //        raw = this.Url,
                    //        host = this.Url.AsArray(),
                    //    }
                    //:
                        new Url()
                        {
                            raw = this.Url,
                            host = this.Url.AsArray(),
                        },
                description = this.Description,
            };
        }
    }

    public class WorkflowStepCustom2Attribute : WorkflowStepCustomAttribute
    {

    }

    public class WorkflowStepCustom3Attribute : WorkflowStepCustomAttribute
    {

    }

    public class WorkflowStepCustom4Attribute : WorkflowStepCustomAttribute
    {

    }
}
