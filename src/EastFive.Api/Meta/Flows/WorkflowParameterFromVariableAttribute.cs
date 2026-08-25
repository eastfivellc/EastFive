using System;
using System.Reflection;

namespace EastFive.Api.Meta.Flows
{
    public class WorkflowParameterFromVariableAttribute : WorkflowParameterBaseAttribute
    {
        public bool Quoted { get; set; } = true;

        public string Value { get; set; }

        protected override string GetValue(ParameterInfo parameter, out bool quoted)
        {
            quoted = this.Quoted;
            return $"{{{{{this.Value}}}}}";
        }
    }
}

