using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.AspNetCore.Builder;

using EastFive.Api;

namespace EastFive.Api.Meta.Postman
{
    public static class PostmanExtensions
    {
        public static IApplicationBuilder UseSchemaHandler(
            this IApplicationBuilder builder, IApplication app)
        {
            return builder.UseMiddleware<SchemaHandler>(app);
        }
    }
}
