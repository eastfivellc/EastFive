using EastFive.Api.Meta.Postman.Resources.Collection;
using EastFive.Api.Resources;
using EastFive.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Meta.Flows
{
    public interface IProvideWorkflowUrl
    {
        Url GetUrl(Api.Resources.Method method, QueryItem[] queryItems);
    }
}
