using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Api.Meta.Flows;

namespace EastFive.Api.Meta.Postman
{
    [FunctionViewController(
        Route = "PostmanCollection",
        Namespace = "meta")]
    public class PostmanCollection : IReferenceable
    {
        #region Properties

        [JsonIgnore]
        public Guid id => this.postmanCollectionRef.id;

        public const string IdPropertyName = "id";
        [JsonProperty(PropertyName = IdPropertyName)]
        [ApiProperty(PropertyName = IdPropertyName)]
        public IRef<PostmanCollection> postmanCollectionRef;

        public string version => "v2.1";

        #endregion

        [EastFive.Api.HttpOptions]
        [Unsecured("Allow all origins to access Postman collections")]
        public static IHttpResponse ListFlows(
                [OptionalQueryParameter] string flow,
                [OptionalQueryParameter] string collections,
                [OptionalQueryParameter] bool? preferJson,
                HttpApplication httpApp,
            ContentTypeResponse<string[]> onSuccess)
        {
            var lookups = httpApp
                .GetResources()
                .ToArray();

            var manifest = new EastFive.Api.Resources.Manifest(lookups, httpApp);

            var flows = manifest.Routes
                .SelectMany(route => route.Methods)
                .SelectMany(method => method.MethodPoco
                    .GetAttributesInterface<IDefineFlow>(multiple: true)
                    .Select(attr => (method, attr)))
                .GroupBy(methodAndFlow => methodAndFlow.attr.FlowName)
                .Where(grp => grp.Key.HasBlackSpace())
                .Select(grp => grp.Key)
                .ToArray();

            return onSuccess(flows);
        }

        [EastFive.Api.HttpGet]
        [Unsecured("Get Postman collection version information")]
        public static IHttpResponse GetVersion(
                [QueryParameter] string flow,
                [QueryParameter] string version,
                IProvideUrl urlHelper,
                HttpApplication httpApp,
            HtmlResponse onSuccess,
            GeneralFailureResponse onFailure)
        {
            var lookups = httpApp
                .GetResources()
                .ToArray();

            var manifest = new EastFive.Api.Resources.Manifest(lookups, httpApp);

            var latestVersion = manifest.Routes
                .SelectMany(route => route.Methods)
                .SelectMany(method => method.MethodPoco
                    .GetAttributesInterface<IDefineFlow>(multiple: true)
                    .Select(attr => (method, attr)))
                .GroupBy(methodAndFlow => methodAndFlow.attr.FlowName)
                .Where(grp => grp.Key == flow)
                .Select(grp => grp.ToArray())
                .SelectMany()
                .First(
                    (x, next) =>
                    {
                        if (x.attr.Version.HasBlackSpace())
                            return x.attr.Version;

                        return next();
                    },
                    () => string.Empty);
            if (string.IsNullOrWhiteSpace(latestVersion))
                return onFailure("Version is not available");

            string getImportLinkForBrowser() => urlHelper
                .Link("meta", typeof(PostmanCollection).Name)
                .AddQueryParameter("flow", flow)
                .AbsoluteUri;

            var sanitizedVerson = System.Web.HttpUtility.HtmlEncode(version);
            var shouldUpdate = !sanitizedVerson.Equals(latestVersion, StringComparison.OrdinalIgnoreCase);
            var status = shouldUpdate
                ? $"An update is available ({latestVersion})"
                : "Collection is up to date";
            var actionRequired = shouldUpdate
                ? $"Re-import collection from:<pre>{getImportLinkForBrowser()}</pre>"
                : $"None";
            var view =
                $"<!DOCTYPE html PUBLIC \"-//W3C//DTD HTML 4.01//EN\" \"http://www.w3.org/TR/html4/strict.dtd\">" +
                $"<html><body><table style=\"text-align:left;width:100%\">" +
                $"<tr><th>Postman Collection</th><th>Version</th><th>Status</th><th>Action Required</th></tr>" +
                $"<tr><td>{flow}</td><td>{sanitizedVerson}</td><td>{status}</td><td>{actionRequired}</td></tr>" +
                $"</table></body></html>";
            return onSuccess(view);
        }

        [EastFive.Api.HttpGet]
        [Unsecured("Provides Postman collection for specified flow")]
        public static IHttpResponse GetSchema(
                [QueryParameter] string flow,
                [OptionalQueryParameter]string collections,
                [OptionalQueryParameter]bool? preferJson,
                IProvideUrl urlHelper,
                IInvokeApplication invokeApplication,
                HttpApplication httpApp, IHttpRequest request, IProvideUrl url,
            ContentTypeResponse<Resources.Collection.Collection> onSuccess,
            NotFoundResponse onNotFound)
        {
            // Prefer a compiler-checked scripted flow when one is defined for this name; fall
            // back to the legacy attribute-driven manifest path otherwise.
            if (EastFive.Api.Meta.Flows.Scripted.FlowScriptReader.TryGetCollection(flow, out var scriptedCollection))
                return onSuccess(scriptedCollection);

            var lookups = httpApp
                .GetResources()
                .Where(
                    resource =>
                    {
                        if (collections.IsNullOrWhiteSpace())
                            return true;
                        var collection = resource.TryGetAttributeInterface(
                                out EastFive.Api.Meta.OpenApi.IDocumentOpenApiRoute documentOpenApiRoute) ?
                            documentOpenApiRoute.Collection
                            :
                            resource.Namespace;
                        return collection.StartsWith(collections, StringComparison.OrdinalIgnoreCase);
                    })
                .ToArray();
            var manifest = new EastFive.Api.Resources.Manifest(lookups, httpApp);

            return manifest.Routes
                .SelectMany(route => route.Methods)
                .SelectMany(method => method.MethodPoco
                    .GetAttributesInterface<IDefineFlow>(multiple:true)
                    .Select(attr => (method, attr)))
                .GroupBy(methodAndFlow => methodAndFlow.attr.FlowName)
                .Where(grp => grp.Key.HasBlackSpace())
                .Where(grp => grp.Key.Equals(flow, StringComparison.OrdinalIgnoreCase))
                .First(
                    (methodAndFlowGrp, next) =>
                    {
                        var flowVersion = methodAndFlowGrp
                            .First(
                                (x, xNext) =>
                                {
                                    if (x.attr.Version.HasBlackSpace())
                                        return x.attr.Version;
                                    return xNext();
                                },
                                () => default(string));

                        string getImportLinkForPostman()
                        {
                            var link = urlHelper
                                .Link("meta", typeof(PostmanCollection).Name)
                                .AddQueryParameter("flow", flow);                                
                            return $"**Import Link**: " + EastFive.Api.Meta.Postman.Resources.Collection.Url.VariableHostName + link.PathAndQuery;
                        }

                        string getCheckForLatestLink()
                        {
                            var link = urlHelper
                                .Link("meta", typeof(PostmanCollection).Name)
                                .AddQueryParameter("flow", flow)
                                .AddQueryParameter("version", flowVersion);
                            return $"[check for latest]({EastFive.Api.Meta.Postman.Resources.Collection.Url.VariableHostName + link.PathAndQuery})";
                        }

                        var description = flowVersion.HasBlackSpace()
                            ? $"**Version**: {flowVersion} {getCheckForLatestLink()}\n\n{getImportLinkForPostman()}"
                            : getImportLinkForPostman();

                        var info = new Resources.Collection.Info
                        {
                            _postman_id = Guid.NewGuid(),
                            name = methodAndFlowGrp.Key,
                            description = description,
                            schema = "https://schema.getpostman.com/json/collection/v2.1.0/collection.json",
                        };

                        var customItems = manifest.Routes
                            .SelectMany(route => route.Type.GetAttributesInterface<IDefineFlow>(multiple: true))
                            .Select(flowAttr => (flowAttr, flowAttr.GetItem(default(Api.Resources.Method),
                                preferJson.HasValue? preferJson.Value : false)))
                            .Where(grp => grp.Item1.FlowName.Equals(flow, StringComparison.OrdinalIgnoreCase))
                            .ToArray();

                        var items = methodAndFlowGrp
                            .Select(
                                methodAndFlow => (methodAndFlow.attr, methodAndFlow.attr.GetItem(methodAndFlow.method,
                                    preferJson.HasValue ? preferJson.Value : false)))
                            .Concat(customItems)
                            .OrderBy(methodAndFlow => methodAndFlow.Item1.Step)
                            .GroupBy(methodAndFlow => methodAndFlow.Item1.GetScope())
                            .Select(scopeGrp =>
                            {
                                var scope = scopeGrp.Key;
                                if (string.IsNullOrWhiteSpace(scope))
                                    return scopeGrp.Select(tpl => tpl.Item2).ToArray();

                                return new EastFive.Api.Meta.Postman.Resources.Collection.Item
                                {
                                    name = scope,
                                    item = scopeGrp.Select(tpl => tpl.Item2).ToArray(),
                                }.AsArray();
                            })
                            .SelectMany()
                            //.Select(tpl => tpl.Item2)
                            .ToArray();

                        var collection = new Resources.Collection.Collection()
                        {
                            info = info,
                            item = items,
                        };
                        return onSuccess(collection);
                    },
                    () => onNotFound());
        }
    }
}
