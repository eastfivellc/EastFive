using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;

using EastFive.Linq;
using EastFive.Extensions;
using EastFive.Linq.Async;
using EastFive.Reflection;

namespace EastFive.Api
{
    [StatusCodeResponse(StatusCode = HttpStatusCode.NoContent)]
    public delegate IHttpResponse NoContentResponse();
}
