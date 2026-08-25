using System;

using EastFive.Api.Resources;

namespace EastFive.Api.Meta.Flows
{
    public class WorkflowVariableFromHeaderAttribute
        : System.Attribute, IDefineWorkflowScriptResponse
    {
        public string VariableName { get; set; }

        public string HeaderKey { get; set; }

        public WorkflowVariableFromHeaderAttribute(string variableName, string headerKey)
        {
            this.VariableName = variableName;
            this.HeaderKey = headerKey;
        }

        public string[] GetInitializationLines(Response response, Method method)
        {
            var lineVarHeaders = "var headers = {};\r";
            var linePopulateHeaders = "pm.response.headers.all().forEach((header) => { headers[header.key] = header.value });\r";

            return new string[] { lineVarHeaders, linePopulateHeaders };
        }

        public string[] GetScriptLines(Response response, Method method)
        {
            var interstatialVariableName = $"headerParam_{this.VariableName}";
            var lineExtract = $"var {interstatialVariableName} = headers[\"{this.HeaderKey}\"];\r";
            var lineMakeGlobal = $"pm.environment.set(\"{this.VariableName}\", {interstatialVariableName});\r";
            return new string[] { lineExtract, lineMakeGlobal, };
        }
    }
}
