using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using EastFive.Api.Resources;
using System.Threading.Tasks;
using System.Net;
using EastFive.Linq;
using EastFive;
using EastFive.Extensions;
using EastFive.Api;
using System.Reflection;
using System.Linq.Expressions;
using System.Net.Http;
using EastFive.Collections.Generic;
using EastFive.Reflection;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Mvc.Routing;

namespace EastFive.Api
{
    public static class UrlExtensions
    {
        public static void AssignQueryValue<T>(this T param, T value)
        {

        }

        public static void AssignNameQueryParameterValue<T>(this string param, T value)
        {

        }

        public static Uri GetLocation<TResource>(this IProvideUrl url,
            Expression<Action<TResource>> param1,
            IApiApplication application,
            string routeName = "DefaultApi")
        {
            return url.GetLocation(
                new Expression<Action<TResource>>[] { param1 },
                application,
                routeName);
        }

        public static Uri GetLocation<TResource>(this IProvideUrl url,
            Expression<Action<TResource>> param1,
            Expression<Action<TResource>> param2,
            IApiApplication application,
            string routeName = "DefaultApi")
        {
            return url.GetLocation(
                new Expression<Action<TResource>>[] { param1, param2 },
                application,
                routeName);
        }

        public static Uri GetLocation<TResource>(this IProvideUrl url,
            Expression<Action<TResource>>[] parameters,
            IApiApplication application,
            string routeName = "DefaultApi")
        {
            var baseUrl = url.GetLocation(typeof(TResource), routeName);
            return baseUrl.SetParameters(parameters, application, routeName: routeName);
        }

        public static Uri SetParameters<TResource>(this Uri baseUrl,
            Expression<Action<TResource>>[] parameters,
            IApiApplication application,
            string routeName = "DefaultApi")
        {
            var queryParams = parameters
                .Select(param => param.GetUrlAssignment(
                    (queryParamName, value) =>
                    {
                        return queryParamName
                            .PairWithValue((string)application.CastResourceProperty(value, typeof(String)));
                    }))
                .ToDictionary();

            var queryUrl = baseUrl.SetQuery(queryParams);
            return queryUrl;
        }

        public static Uri GetLocation(this IProvideUrl url, Type controllerType,
            string routeName = default)
        {
            if(routeName.IsNullOrWhiteSpace())
            {
                routeName = controllerType.GetCustomAttribute<FunctionViewControllerAttribute, string>(
                    (attr) => attr.Namespace,
                    () => "api");
            }

            var controllerName = controllerType.GetCustomAttribute<FunctionViewControllerAttribute, string>(
                (attr) => attr.Route,
                () => controllerType.Name
                    .TrimEnd("Controller",
                        (trimmedName) => trimmedName,
                        (originalName) => originalName)
                    .ToLower());

            var location = url.Link(routeName, controllerName:controllerName);
            return location;
        }

        public static Uri GetLocation<TController>(this IProvideUrl url,
            string routeName = "DefaultApi")
        {
            return url.GetLocation(typeof(TController), routeName:routeName);
        }

        public static Uri GetLocationIfRelative(this IProvideUrl url, string absoluteOrRelative)
        {
            bool isImplicit()
            {
                if (!absoluteOrRelative.HasBlackSpace())
                    return false;

                absoluteOrRelative = absoluteOrRelative.TrimStart();
                return absoluteOrRelative.StartsWith("\\") ||
                    absoluteOrRelative.StartsWith("/");
            }

            // must provide the scheme rather than using anything implicit
            if (!isImplicit() && Uri.TryCreate(absoluteOrRelative, UriKind.Absolute, out Uri absoluteUri))
                return absoluteUri;

            return url.Combine(absoluteOrRelative);
            
        }

        public static Uri GetUrn(this Type controllerType,
            string urnNamespace)
        {
            var controllerName =
                controllerType.Name.TrimEnd("Controller",
                    (trimmedName) => trimmedName, (originalName) => originalName);

            if (controllerType.ContainsCustomAttribute<FunctionViewControllerAttribute>())
            {
                var fvcAttr = controllerType.GetCustomAttribute<FunctionViewControllerAttribute>();
                if (fvcAttr.Route.HasBlackSpace())
                    controllerName = fvcAttr.Route;
            }

            var urn = new Uri("urn:" + urnNamespace + ":" + controllerName);
            return urn;
        }

        public static Uri GetLocation<TController>(this IProvideUrl url,
            Guid? idMaybe,
            string routeName = default(string))
        {
            if (idMaybe.HasValue)
                return url.GetLocation<TController>(idMaybe.Value, routeName);
            return default(Uri);
        }

        public static Uri GetLocation(this IProvideUrl url, Type controllerType, Guid id,
            string routeName = "DefaultApi")
        {
            if (String.IsNullOrWhiteSpace(routeName))
                routeName = "DefaultApi";

            var controllerName =
                controllerType.Name.TrimEnd("Controller",
                    (trimmedName) => trimmedName, (originalName) => originalName);
            var location = url.Link(routeName, controllerName: controllerName, id:id.ToString());
            return location;
        }

        public static Uri GetLocation<TController>(this IProvideUrl url,
            Guid id,
            string routeName = default(string))
        {
            return url.GetLocation(typeof(TController), id, routeName);
        }
        
        public static Uri GetLocation<TController>(this IProvideUrl url,
            string action,
            string routeName = "DefaultApi")
        {
            var controllerName =
                typeof(TController).Name.TrimEnd("Controller",
                    (trimmedName) => trimmedName, (originalName) => originalName);
            var location = url.Link(routeName, controllerName:controllerName, action:action);
            return location;
        }


    }
}
