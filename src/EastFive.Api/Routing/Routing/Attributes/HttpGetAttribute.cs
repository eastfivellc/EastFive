using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method)]
    public class HttpGetAttribute : HttpVerbAttribute
    {
        public override string Method => System.Net.Http.HttpMethod.Get.Method;
    }
}
