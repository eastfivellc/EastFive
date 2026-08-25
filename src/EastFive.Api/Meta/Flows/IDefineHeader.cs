using EastFive.Api.Meta.Postman.Resources.Collection;
using EastFive.Api.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Meta.Flows
{
    public interface IDefineHeader
    {
        Header GetHeader(Api.Resources.Method method, ParameterInfo parameter);
    }

    public class WorkflowHeaderRequiredAttribute : Attribute, IDefineHeader
    {
        private string headerKey;
        private string headerValue;

        public WorkflowHeaderRequiredAttribute(string headerKey, string headerValue)
        {
            this.headerKey = headerKey;
            this.headerValue = headerValue;
        }

        public Header GetHeader(Api.Resources.Method method, ParameterInfo parameter)
        {
            return new Header()
            {
                key = headerKey,
                value = headerValue,
                type = "text",
            };
        }
    }
}
