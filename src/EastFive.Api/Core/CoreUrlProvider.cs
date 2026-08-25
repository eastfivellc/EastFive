using Microsoft.AspNetCore.Http;
using System;
using System.IO;

namespace EastFive.Api.Core
{
    public class CoreUrlProvider : IProvideUrl
    {
        private HttpContext context;

        public CoreUrlProvider(HttpContext context)
        {
            this.context = context;
        }

        public Uri Link(string routeName, string controllerName, string action = null, string id = null)
        {
            if ("DefaultApi".Equals(routeName))
                routeName = "api";

            var request = this.context.Request;
            var urlString = $"{request.Scheme}://{context.Request.Host}/{routeName}/{controllerName}";
            if (action.HasBlackSpace())
                urlString = urlString + $"/{action}";
            if (id.HasBlackSpace())
                urlString = urlString + $"/{id}";
            return new Uri(urlString, UriKind.Absolute);
        }

        public Uri Combine(string rightPart)
        {
            var request = this.context.Request;
            var leftPart = $"{request.Scheme}://{context.Request.Host}";
            if (rightPart.IsNullOrWhiteSpace())
                return new Uri(leftPart, UriKind.Absolute);

            rightPart = rightPart.TrimStart();
            while (rightPart.StartsWith("/") || rightPart.StartsWith("\\"))
                rightPart = rightPart.Substring(1);
                
            var absolutePath = Path.Combine(leftPart, rightPart).Replace("\\", "/");
            return new Uri(absolutePath, UriKind.Absolute);
        }
    }
}
