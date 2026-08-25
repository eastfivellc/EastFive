using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class ResourceNotFoundException : Exception, IHttpResponseMessageException
    {
        public ResourceNotFoundException() : base()
        {
        }

        public ResourceNotFoundException(string message) : base(message)
        {
        }

        public static TResult StorageGetAsync<TResult>()
        {
            throw new ResourceNotFoundException();
        }

        public IHttpResponse CreateResponseAsync(IApplication httpApp,
            IHttpRequest request, Dictionary<string, object> queryParameterOptions,
            MethodInfo method, object[] methodParameters)
        {
            var response = request.CreateResponse(System.Net.HttpStatusCode.NotFound,
                this.StackTrace);
            return response.AddReason(this.Message);
        }
    }

    
}
