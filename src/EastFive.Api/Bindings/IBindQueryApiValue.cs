using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public interface IBindQueryApiValue
    {
        TResult ParseContentDelegate<TResult>(IDictionary<string, string> pairs,
                ParameterInfo parameterInfo,
                IApplication httpApp, IHttpRequest routeData,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure);
    }
}
