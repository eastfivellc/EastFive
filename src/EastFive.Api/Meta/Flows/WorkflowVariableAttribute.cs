using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using EastFive.Api.Resources;
using EastFive.Extensions;

namespace EastFive.Api.Meta.Flows
{
    public interface IDefineWorkflowResponseVariable
    {
        string[] GetInitializationLines(Response response, Method method);

        string[] GetScriptLines(string variableName, string propertyName,
            Response response, Method method);
    }

    public class WorkflowVariableAttribute
        : Attribute, IDefineWorkflowScriptParam, IDefineWorkflowScriptResponse
    {
        public string VariableName { get; set; }

        public string PropertyName { get; set; }

        public WorkflowVariableAttribute(string variableName, string propertyName)
        {
            this.VariableName = variableName;
            this.PropertyName = propertyName;
        }

        public WorkflowVariableAttribute(string variableName)
        {
            this.VariableName = variableName;
            this.PropertyName = default(String);
        }

        #region IDefineWorkflowScriptParam

        private static bool IsQuery(Parameter param) =>
            param.PocoParameter.ContainsAttributeInterface<IBindQueryApiValue>();

        private static bool IsJson(Parameter param) =>
            param.PocoParameter.ContainsAttributeInterface<IBindJsonApiValue>();

        public string[] GetInitializationLines(Parameter param, Method method)
        {
            if (IsQuery(param))
            {
                return new string[]
                {
                    "var query = {};\r",
                    "pm.request.url.query.all().forEach((param) => { query[param.key] = param.value});\r",
                };
            }

            if (IsJson(param))
            {
                return new string[]
                {
                    "let commentFreeJson = pm.request.body.raw.replace(/\\\\\"|\"(?:\\\\\"|[^\"])*\"|(\\/\\/.*|\\/\\*[\\s\\S]*?\\*\\/)/g, (m, g) => g ? \"\" : m);\r",
                    "let requestResource = JSON.parse(commentFreeJson);\r",
                };
            }

            var member = param.PocoParameter.Member;
            return new string[]
            {
                $"// Cannot generate workflow variable {this.VariableName} from {this.PropertyName}\r",
                $"// for {param.Name} found on {member.DeclaringType.FullName}..{member.Name}.",
            };
        }

        public string[] GetScriptLines(Parameter param, Method method)
        {
            var propertyName = this.PropertyName.HasBlackSpace() ?
                this.PropertyName
                :
                param.PocoParameter.TryGetAttributeInterface(out IBindApiValue apiValueBinder) ?
                    apiValueBinder.GetKey(param.PocoParameter)
                    :
                    param.Name;

            if (IsQuery(param))
            {
                
                var interstationVariableName = $"queryParam_{this.VariableName}";
                var lineExtract = $"var {interstationVariableName} = query[\"{propertyName}\"];\r";
                var lineMakeGlobal = $"pm.environment.set(\"{this.VariableName}\", {interstationVariableName});\r";
                return new string[] { lineExtract, lineMakeGlobal, };
            }

            if (IsJson(param))
            {
                return $"pm.environment.set(\"{this.VariableName}\", requestResource.{propertyName});\r".AsArray();
            }


            var member = param.PocoParameter.Member;
            return new string[]
            {
                $"// Cannot generate workflow variable {this.VariableName} from {propertyName}\r",
                $"// for {param.Name} found on {member.DeclaringType.FullName}..{member.Name}.",
            };
        }

        #endregion

        #region IDefineWorkflowScriptResponse

        public string[] GetInitializationLines(Response response, Method method)
        {
            if (response.ParamInfo.ParameterType.TryGetAttributeInterface(out IDefineWorkflowResponseVariable typeResponse))
                return typeResponse.GetInitializationLines(response, method);

            var member = response.ParamInfo.Member;
            return new string[]
            {
                $"// Cannot generate workflow variable {this.VariableName} from {this.PropertyName}\r",
                $"// for {response.ParamInfo.Name} found on {member.DeclaringType.FullName}..{member.Name}.",
            };
        }

        public string[] GetScriptLines(Response response, Method method)
        {
            if (response.ParamInfo.ParameterType.TryGetAttributeInterface(out IDefineWorkflowResponseVariable typeResponse))
                return typeResponse.GetScriptLines(this.VariableName, this.PropertyName, response, method);

            var member = response.ParamInfo.Member;
            return new string[] { };
        }

        #endregion
    }

    public class WorkflowVariable2Attribute : WorkflowVariableAttribute
    {
        public WorkflowVariable2Attribute(string variableName, string propertyName)
            : base(variableName, propertyName)
        {

        }
    }

    public class WorkflowVariable3Attribute : WorkflowVariableAttribute
    {
        public WorkflowVariable3Attribute(string variableName, string propertyName)
            : base(variableName, propertyName)
        {

        }
    }
}
