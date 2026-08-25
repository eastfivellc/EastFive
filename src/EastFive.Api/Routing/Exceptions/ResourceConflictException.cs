using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;

namespace EastFive.Api
{

    public class ResourceConflictException : ArgumentException, IHttpResponseMessageException
    {
        public ResourceConflictException() : base()
        {

        }

        public ResourceConflictException(string message) : base(message)
        {

        }

        public ResourceConflictException(string message, Exception innerException) : base(message, innerException)
        {

        }

        public ResourceConflictException(string message, string paramName) : base(message, paramName)
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
