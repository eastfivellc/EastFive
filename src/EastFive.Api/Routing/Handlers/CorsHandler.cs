using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using EastFive;
using EastFive.Linq;
using EastFive.Extensions;
using EastFive.Web.Configuration;

namespace EastFive.Api
{
    public class CorsHandlerAttribute : Attribute, IHandleRoutes
    {
        public Task<IHttpResponse> HandleRouteAsync(Type controllerType, IInvokeResource resourceInvoker,
            IApplication httpApp, IHttpRequest request,
            RouteHandlingDelegate continueExecution)
        {
            Func<Task<IHttpResponse>> skip = 
                () => continueExecution(controllerType, httpApp, request);
            
            if(!AppSettings.CorsCorrection.ConfigurationBoolean(s =>s, onNotSpecified:() => false))
                return skip();

            // Configs to set:
            //
            // EastFive.Api.CorsCorrection=true
            // cors:Origins=https://myserver.com,etc.  (localhost included by default so this app setting can remain absent/unconfigured if just need localhost)
            // cors:MaxAgeSeconds=60                   (default is 5 seconds if not set)
            //
            return GetResponse();

            string GetAllowedOrigin()
            {
                // accept localhost (all ports)
                // accept request server
                // accept any additional servers in config
                request.Headers.TryGetValue("Origin", out string[] reqOrigins);
                var localhostAuthorities = reqOrigins
                    .NullToEmpty()
                    .SelectMany(reqOrigin => reqOrigin.Split(','.AsArray(), StringSplitOptions.RemoveEmptyEntries))
                    .Where(
                        (reqOrigin) =>
                        {
                            if (!Uri.TryCreate(reqOrigin, UriKind.Absolute, out Uri reqOriginUri))
                                return false;

                            return reqOriginUri.GetLeftPart(UriPartial.Authority).IndexOf("localhost", StringComparison.OrdinalIgnoreCase) != -1;
                        });
                var requestAuthority = request.RequestUri.GetLeftPart(UriPartial.Authority);
                var corsAuthorities = "cors:Origins".ConfigurationString(
                    (v) => v.Split(','.AsArray(), StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray(),
                    (why) => new string[] { });
                var allowableOriginValues = localhostAuthorities
                    .Append(requestAuthority)
                    .Concat(corsAuthorities)
                    .Distinct()
                    .ToArray();
                var allowedOrigin = allowableOriginValues
                    .NullToEmpty()
                    .First(
                        (allowed, next) =>
                        {
                            if (!reqOrigins.NullToEmpty().Contains(allowed, StringComparer.OrdinalIgnoreCase))
                                return next();

                            return allowed;
                        },
                        () => default(string));
                return allowedOrigin;
            }

            string GetAllowedMethods()
            {
                // accept OPTIONS
                // accept any additional methods in config
                request.Headers.TryGetValue("Access-Control-Request-Method", out string[] reqMethod);
                return reqMethod
                    .NullToEmpty()
                    .Append("OPTIONS")
                    .Distinct()
                    .Join(",");
            }

            string GetAllowedHeaders()
            {
                // accept any headers requested
                request.Headers.TryGetValue("Access-Control-Request-Headers", out string[] reqHeaders);
                return reqHeaders
                    .NullToEmpty()
                    .Distinct()
                    .Join(",");                
            }

            string GetMaxAgeSeconds()
            {
                return "cors:MaxAgeSeconds"
                    .ConfigurationLong(
                        (v) => v,
                        (why) => 5,
                        () => 5) 
                    .ToString();
            }

            Task<IHttpResponse> GetResponse()
            {
                if (request.Method.Method.ToLower() != HttpMethod.Options.Method.ToLower())
                    return skip();

                var allowedOrigin = GetAllowedOrigin();
                if (allowedOrigin == default)
                    return request.CreateResponse(System.Net.HttpStatusCode.Forbidden).AddReason("origin not allowed").AsTask();

                var response = request.CreateResponse(System.Net.HttpStatusCode.OK);
                response.SetHeader("Access-Control-Allow-Origin", allowedOrigin);
                response.SetHeader("Access-Control-Allow-Methods", GetAllowedMethods());
                response.SetHeader("Access-Control-Allow-Headers", GetAllowedHeaders());
                response.SetHeader("Vary", "origin");
                response.SetHeader("Access-Control-Max-Age", GetMaxAgeSeconds());
                return response.AsTask();
            }
        }
    }
}
