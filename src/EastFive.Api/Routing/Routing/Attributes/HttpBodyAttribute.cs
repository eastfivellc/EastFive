using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method)]
    public abstract class HttpBodyAttribute : HttpVerbAttribute
    {
        public Type Type { get; set; }
    }
}
