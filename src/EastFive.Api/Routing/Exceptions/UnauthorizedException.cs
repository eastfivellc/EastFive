using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class UnauthorizedException : Exception, IHttpResponseMessageException
    {
        public UnauthorizedException() : base()
        {
        }

        public UnauthorizedException(string message) : base(message)
        {
        }

        public IHttpResponse CreateResponseAsync(IApplication httpApp,
           IHttpRequest request, Dictionary<string, object> queryParameterOptions,
           MethodInfo method, object[] methodParameters)
        {
            return request
                .CreateResponse(System.Net.HttpStatusCode.InternalServerError, this.StackTrace)
                .AddReason(this.Message);
        }
    }
}
