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
    public interface IDefineWorkflowScriptResponse : IDefineWorkflowScript<Response>
    {
        string[] GetScriptLines(Response response, Method method);
    }
}
