using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Net.Http;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Reflection;
using Newtonsoft.Json;
using EastFive.Api.Auth;

namespace EastFive.Api.Resources
{
    [FunctionViewController(Route = "ManifestRoute")]
    public class Route
    {
        public string Namespace { get; set; }

        public string Name { get; set; }

        public Method[] Methods { get; set; }

        public Property[] Properties { get; set; }

        public bool IsEntryPoint { get; set; }

        public Type Type { get; private set; }

        [RequiredClaim(
            System.Security.Claims.ClaimTypes.Role,
            ClaimValues.Roles.SuperAdmin)]
        [EastFive.Api.HttpGet]
        public static IHttpResponse FindAsync(
                IApplication app,
            ContentTypeResponse<Route[]> onContent)
        {
            var lookups = app.Resources.Select(res => res.type).ToArray();
            var manifest = new Manifest(lookups, app as HttpApplication);
            return onContent(manifest.Routes);
        }

        //public Route(Type type, string name, KeyValuePair<HttpMethod, MethodInfo[]>[] methods,
        //    HttpApplication httpApp)
        //{
        //    this.IsEntryPoint = type.ContainsAttributeInterface<IDisplayEntryPoint>();
        //    this.Name = name;
        //    this.Methods = methods
        //        .SelectMany(kvp => kvp.Value.Select(m => m.PairWithKey(kvp.Key)))
        //        .Select(
        //            verb =>
        //            {
        //                return verb.Value
        //                    .GetAttributesInterface<IDocumentMethod>()
        //                    .First(
        //                        (methodDoc, next) => methodDoc.GetMethod(this, verb.Value, httpApp),
        //                        () =>
        //                        {
        //                            var path = new Uri($"/api/{name}", UriKind.Relative);
        //                            return new Method(verb.Key.Method, verb.Value,
        //                                path, httpApp);
        //                        });
        //            })
        //        .ToArray();
        //    this.Properties = methods
        //        .First(
        //            (methodKvp, next) =>
        //            {
        //                return methodKvp.Value
        //                    .First(
        //                        (method, nextInner) =>
        //                        {
        //                            return method.DeclaringType
        //                                .GetPropertyOrFieldMembers()
        //                                .Where(property => property.ContainsCustomAttribute<JsonPropertyAttribute>())
        //                                .Select(member => new Property(member, httpApp))
        //                                .ToArray();
        //                            //return new Property[] { };
        //                        },
        //                        () => new Property[] { });
        //            },
        //            () => new Property[] { });
        //}

        public Route(Type type, string nameSpace, string name, MethodInfo[] methods, MemberInfo[] properties,
            HttpApplication httpApp)
        {
            this.Type = type;
            this.IsEntryPoint = type.ContainsAttributeInterface<IDisplayEntryPoint>();
            this.Namespace = nameSpace;
            this.Name = name;
            this.Methods = methods
                .Where(method => method.ContainsAttributeInterface<IDocumentMethod>())
                .Select(
                    method =>
                    {
                        var docMethod = method.GetAttributesInterface<IDocumentMethod>().First();
                        return docMethod.GetMethod(this, method, httpApp);
                    })
                .ToArray();
            this.Properties = properties
                .Where(method => method.ContainsAttributeInterface<IDocumentProperty>())
                .Select(method => method.GetAttributesInterface<IDocumentProperty>()
                    .First()
                    .GetProperty(method, httpApp))
                .ToArray();
        }

    }
}
