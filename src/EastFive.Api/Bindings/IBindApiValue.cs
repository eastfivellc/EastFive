using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public delegate SelectParameterResult CastDelegate(ParameterInfo parameterInfo,
        Func<object, SelectParameterResult> onCasted,
        Func<string, SelectParameterResult> onFailedToCast);

    public struct BindingData
    {
        public IApplication httpApp;
        public IHttpRequest request;
        public MethodInfo method;
        public IInvokeResource resourceInvoker;

        public ParameterInfo parameterRequiringValidation;

        public CastDelegate fetchQueryParam;
        public CastDelegate fetchBodyParam;
        public CastDelegate fetchDefaultParam;
    }

    public interface IBindApiValue
    {
        string GetKey(ParameterInfo paramInfo);

        SelectParameterResult TryCast(BindingData bindingData);
    }
}
