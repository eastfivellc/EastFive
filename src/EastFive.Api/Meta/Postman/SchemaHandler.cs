using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using EastFive.Api;
using EastFive.Api.Core;
using System.Linq;

namespace EastFive.Api.Meta.Postman
{
    public class SchemaHandler
    {
        private readonly RequestDelegate continueAsync;
        private HttpApplication httpApp;

        public const string YamlMimeType = "text/yaml";

        public SchemaHandler(RequestDelegate next, IApplication app)
        {
            this.continueAsync = next;
            this.httpApp = (app as HttpApplication);
        }

        public async Task InvokeAsync(HttpContext context,
               Microsoft.AspNetCore.Hosting.IWebHostEnvironment environment)
        {
            if (!context.Request.GetAcceptTypes()
                .Select(mt => mt.MediaType.ToLower())
                .Contains(YamlMimeType))
            {
                await continueAsync(context);
                return;
            }

            var lookups = httpApp.GetResources();
            var manifest = new EastFive.Api.Resources.Manifest(lookups, httpApp);
        }
    }
}
