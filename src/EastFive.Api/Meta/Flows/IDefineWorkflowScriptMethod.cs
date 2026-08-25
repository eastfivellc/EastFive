using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using EastFive.Api.Resources;

namespace EastFive.Api.Meta.Flows
{
    public interface IDefineWorkflowScriptMethod : IDefineWorkflowScript<Method>
    {
        string [] GetScriptLines(Method method);
    }
}
