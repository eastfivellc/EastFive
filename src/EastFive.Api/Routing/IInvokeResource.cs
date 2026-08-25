using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public interface IInvokeResource
    {
        string Namespace { get; }

        string Route { get; }

        string ContentType { get; }
    }

    public interface IInvokeExtensions
    {
        KeyValuePair<Type, MethodInfo>[] GetResourcesExtended(Type extensionType);
    }
}
