using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using EastFive;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Reflection;
using EastFive.Serialization;
using EastFive.Api.Resources;

namespace EastFive.Api.Meta.Flows
{
    public class WorkflowVariableArrayPropertyArrayAttribute
        : System.Attribute, IDefineWorkflowScriptResponse
    {
        public string PropertyName { get; set; }

        public string VariableName { get; set; }

        public string[] GetInitializationLines(Response response, Method method)
        {
            return new string[] { };
        }

        public string[] GetScriptLines(Response response, Method method)
        {
            var resourceType = response.ParamInfo.ParameterType.TryGetAttributeInterface(out IProvideResponseType responseTypeProvider) ?
                responseTypeProvider.GetResponseType(response.ParamInfo)
                :
                method.Route.Type;

            var parseLine = "let objArray = pm.response.json();\r";
            var mapLine = $"let propertyArray = objArray.map(a => a.{this.PropertyName});\r";
            var setEnvVariable = $"pm.environment.set(\"{this.VariableName}\", JSON.stringify(propertyArray));\r";

            return new string[]
            {
                parseLine,
                mapLine,
                setEnvVariable,
            };
        }
    }

    public class WorkflowVariableArrayIndexedProperty
        : System.Attribute, IDefineWorkflowScriptResponse
    {
        public string PropertyName { get; set; }

        public string VariableName { get; set; }

        public int Index { get; set; }

        public string[] GetInitializationLines(Response response, Method method)
        {
            return new string[] { };
        }

        public string[] GetScriptLines(Response response, Method method)
        {
            var resourceType = response.ParamInfo.ParameterType.TryGetAttributeInterface(out IProvideResponseType responseTypeProvider) ?
                responseTypeProvider.GetResponseType(response.ParamInfo)
                :
                method.Route.Type;

            var parseLine = "let objArray = pm.response.json();\r";
            var mapLine = $"let propertyArray = objArray.map(a => a.{this.PropertyName});\r";
            var selectedValue = $"let selectedValue = propertyArray[{this.Index}];\r";
            var setEnvVariable = $"pm.environment.set(\"{this.VariableName}\", selectedValue);\r";

            return new string[]
            {
                parseLine,
                mapLine,
                selectedValue,
                setEnvVariable,
            };
        }
    }

    public class WorkflowVariableArrayIndexedValue
        : System.Attribute, IDefineWorkflowScriptResponse
    {
        public string VariableName { get; set; }

        public int Index { get; set; }

        public string[] GetInitializationLines(Response response, Method method)
        {
            return new string[] { };
        }

        public string[] GetScriptLines(Response response, Method method)
        {
            var resourceType = response.ParamInfo.ParameterType.TryGetAttributeInterface(out IProvideResponseType responseTypeProvider) ?
                responseTypeProvider.GetResponseType(response.ParamInfo)
                :
                method.Route.Type;

            var parseLine = "let objArray = pm.response.json();\r";
            var selectedValue = $"let selectedValue = objArray[{this.Index}];\r";
            var setEnvVariable = $"pm.environment.set(\"{this.VariableName}\", selectedValue);\r";

            return new string[]
            {
                parseLine,
                selectedValue,
                setEnvVariable,
            };
        }
    }
}
