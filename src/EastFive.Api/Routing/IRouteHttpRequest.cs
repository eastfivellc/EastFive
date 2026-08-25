using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace EastFive.Api.Routing
{
    public interface IRouteHttpRequest : IHttpRequest
    {
        public MethodInfo[] ExtensionMethods { get; }

        public Type Type { get; }
    }
}
