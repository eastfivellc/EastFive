using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class ConfigurationException : Web.ConfigurationException, IHttpResponseMessageException
    {
        public ConfigurationException(string parameterName, Type parameterType, string message)
            : base(parameterName, parameterType, message)
        {
        }

        public static TResult OnApiConfigurationFailure<TResult>(string parameterName, Type parameterType, string message)
        {
            throw new ConfigurationException(parameterName, parameterType, message);
        }

        public static TResult OnApiConfigurationFailureWhy<TResult>(string message)
        {
            throw new ConfigurationException(string.Empty, typeof(string), message);
        }

        public IHttpResponse CreateResponseAsync(IApplication httpApp, IHttpRequest request,
            Dictionary<string, object> queryParameterOptions, MethodInfo method, object[] methodParameters)
        {
            var response = request.CreateResponse(System.Net.HttpStatusCode.InternalServerError, this.StackTrace);
            return response.AddReason(this.Message);
        }
    }
}
