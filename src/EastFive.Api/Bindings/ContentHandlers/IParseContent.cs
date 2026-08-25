using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public interface IParseContent
    {
        bool DoesParse(IHttpRequest routeData);

        Task<IHttpResponse> ParseContentValuesAsync(
            IApplication httpApp, IHttpRequest routeData,
            Func<
                CastDelegate,
                string[],
                Task<IHttpResponse>> onParsedContentValues);
    }
}
