using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public interface IProvideSerialization
    {
        string MediaType { get; }

        string ContentType { get; }

        double GetPreference(IHttpRequest request);

        Task SerializeAsync(Stream responseStream,
            IApplication httpApp, IHttpRequest request, ParameterInfo paramInfo, object obj);
    }
}
