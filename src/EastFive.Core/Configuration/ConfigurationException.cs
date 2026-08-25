using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Web
{
    public class ConfigurationException : Exception
    {

        public string ParameterName { get; private set; }

        public Type ParameterType { get; private set; }

        public ConfigurationException(string parameterName, Type parameterType, string message)
            : base(message)
        {
            this.ParameterName = parameterName;
            this.ParameterType = parameterType;
        }

        public static TResult OnConfigurationFailure<TResult>(string parameterName, Type parameterType, string message)
        {
            throw new ConfigurationException(parameterName, parameterType, message);
        }

        public static TResult OnConfigurationFailureWhy<TResult>(string message)
        {
            throw new ConfigurationException(string.Empty, typeof(string), message);
        }
    }
}
