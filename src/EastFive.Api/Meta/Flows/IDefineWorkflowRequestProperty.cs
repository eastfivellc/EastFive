using EastFive.Api.Meta.Postman.Resources.Collection;
using EastFive.Api.Resources;
using EastFive.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Meta.Flows
{
    public interface IDefineWorkflowRequestProperty
    {
        public string Scope { get; }

        void AddProperties(JsonWriter requestObj, ParameterInfo parameter);
    }

    public interface IDefineWorkflowRequestPropertyFormData : IDefineWorkflowRequestProperty
    {
        FormData[] GetFormData(ParameterInfo parameter);
    }

    
}
