using System;

using EastFive;
using EastFive.Api.Resources;
using EastFive.Extensions;

namespace EastFive.Api.Meta.Flows
{
    public class WorkflowVariableRedirectUrlAttribute : System.Attribute, IDefineWorkflowScriptResponse
    {
        public string VariableName { get; set; }

        public string[] GetInitializationLines(Response response, Method method)
        {
            return "let redirectStringToExportToEnv = pm.response.headers.get(\"Location\");".AsArray();
        }

        public string[] GetScriptLines(Response response, Method method)
        {
            return $"pm.environment.set(\"{VariableName}\", redirectStringToExportToEnv);".AsArray();
        }
    }
}

