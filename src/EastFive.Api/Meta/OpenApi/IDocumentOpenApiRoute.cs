using System;
using System.Collections.Generic;
using System.Text;

namespace EastFive.Api.Meta.OpenApi
{
    public interface IDocumentOpenApiRoute
    {
        public string Collection { get; }
    }

    public class OpenApiRouteAttribute : Attribute, IDocumentOpenApiRoute
    {
        public string Collection { get; set; }
    }
}
