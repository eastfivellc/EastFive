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
using EastFive.Linq.Expressions;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Mvc.Routing;
using EastFive.Reflection;

namespace EastFive.Api
{
    public static class ResourceExtensions
    {
        public static Uri GetLocation<TResource>(this UrlHelper url,
            Expression<Action<TResource>> param1,
            IApiApplication application,
            string routeName = "DefaultApi")
        {
            return url.GetLocation(
                new Expression<Action<TResource>>[] { param1 },
                application,
                routeName);
        }

        public static Uri GetLocation<TResource>(this UrlHelper url,
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

        public static Uri GetLocation<TResource>(this UrlHelper url,
            Expression<Action<TResource>>[] parameters,
            IApiApplication application,
            string routeName = "DefaultApi")
        {
            var baseUrl = url.GetLocation(typeof(TResource), routeName);
            return baseUrl.SetParameters(parameters, application, routeName: routeName);
        }
        
        public static TResult GetUrlAssignment<TObject, TResult>(this Expression<Action<TObject>> expression,
            Func<string, object, TResult> onAssignmentResolved,
            Func<string, TResult> onFailure = default(Func<string, TResult>))
        {
            var body = expression.Body;
            var methodCall = body as MethodCallExpression;

            var valueBeingAssigned = methodCall.Arguments[0];
            if (valueBeingAssigned is MemberExpression)
            {
                var memberInfo = (valueBeingAssigned as MemberExpression).Member;
                var valueResolved = methodCall.Arguments[1].Resolve();

                var memberName = memberInfo.GetCustomAttribute<JsonPropertyAttribute, string>(
                    jsonAttr => jsonAttr.PropertyName,
                    () => memberInfo.Name);
                return onAssignmentResolved(memberName, valueResolved);
            }

            {
                var valueBeingAssignedName = methodCall.Arguments[0].Resolve();
                var valueResolved = methodCall.Arguments[1].Resolve();
                return onAssignmentResolved(valueBeingAssignedName as string, valueResolved);
            }
        }

        public static Uri GetLocation(this UrlHelper url, Type controllerType,
            string routeName = "DefaultApi")
        {
            if (String.IsNullOrWhiteSpace(routeName))
            {
                    routeName = "DefaultApi";
            }

            var controllerName = controllerType.GetCustomAttribute<FunctionViewControllerAttribute, string>(
                (attr) => attr.Route,
                () => controllerType.Name
                    .TrimEnd("Controller",
                        (trimmedName) => trimmedName,
                        (originalName) => originalName)
                    .ToLower());

            var location = url.Link(routeName, new { Controller = controllerName });
            return new Uri(location);
        }

        public static Uri GetLocation<TController>(this UrlHelper url,
            string routeName = "DefaultApi")
        {
            return url.GetLocation(typeof(TController), routeName:routeName);
        }

        public static IEnumerable<Guid> ParseGuidString(this string guidString)
        {
            if (String.IsNullOrWhiteSpace(guidString))
                return new Guid[] { };

            var guids = guidString.Split(new char[','])
                .Where(guidStringCandidate => { Guid g; return Guid.TryParse(guidStringCandidate, out g); })
                .Select(guidStringCandidate => { Guid g; Guid.TryParse(guidStringCandidate, out g); return g; });
            return guids;
        }

        public static TResult ParseGuidString<TResult>(this string guidString,
            Func<IEnumerable<Guid>, TResult> multiple,
            Func<TResult> none)
        {
            if (String.IsNullOrWhiteSpace(guidString))
                return none();

            var guids = guidString.Split(new char[] { ',' })
                .Where(guidStringCandidate =>
                {
                    Guid g;
                    var validGuid = Guid.TryParse(guidStringCandidate, out g);
                    return validGuid;
                })
                .Select(guidStringCandidate => { Guid g; Guid.TryParse(guidStringCandidate, out g); return g; })
                .ToArray();
            return multiple(guids);
        }

        private static TResult ParseMethod<TResult>(
            object [] queryParams,
            MethodCallExpression methodCallExpression,
            Func<string, IDictionary<string, object>, TResult> onParsed)
        {
            var controllerType = methodCallExpression.Method.DeclaringType;
            var controllerName = controllerType.GetCustomAttribute<FunctionViewControllerAttribute, string>(
                (attr) => attr.Route,
                () => controllerType.Name.TrimEnd("Controller",
                    (trimmedName) => trimmedName, (originalName) => originalName)).ToLower();

            // TODO: Check if method has Get attribute?

            var queryParameters = methodCallExpression.Arguments
                .Zip(methodCallExpression.Method.GetParameters(), (k1, k2) => k1.PairWithValue(k2))
                .Where(arg => arg.Key is ParameterExpression)
                .Select(arg => (arg.Key as ParameterExpression).PairWithValue(arg.Value))
                .Zip(queryParams, (arg, queryParam) => arg.Value.Name.PairWithValue(queryParam)) // TODO: Change to call to ConvertToQueryParameter()
                // .Select(arg => arg.Value.Name.PairWithValue((object)queryParam1)) // TODO: Change to call to ConvertToQueryParameter()
                .Append("Controller".PairWithValue((object)controllerName))
                .ToDictionary();
            // TODO: Check if query param has DefaultId attribute 

            return onParsed(controllerName, queryParameters);
        }

        private static TResult ParseMethod<T1, TResult>(
            T1 queryParam1,
            Expression<Func<T1, Task<HttpResponseMessage>>> queryMethodExpression,
            Func<string, IDictionary<string, object>, TResult> onParsed)
        {
            var methodCallExpression = queryMethodExpression.Body as MethodCallExpression;
            var controllerType = methodCallExpression.Method.DeclaringType;
            var controllerName = controllerType.GetCustomAttribute<FunctionViewControllerAttribute, string>(
                (attr) => attr.Route,
                () => controllerType.Name.TrimEnd("Controller",
                    (trimmedName) => trimmedName, (originalName) => originalName)).ToLower();

            // TODO: Check if method has Get attribute?

            var queryParameters = methodCallExpression.Arguments
                .Zip(methodCallExpression.Method.GetParameters(), (k1, k2) => k1.PairWithValue(k2))
                .Where(arg => arg.Key is ParameterExpression)
                // TODO: Zip with T1, T2... etc when passes as a list 
                .Select(arg => (arg.Key as ParameterExpression).PairWithValue(arg.Value))
                .Select(arg => arg.Value.Name.PairWithValue((object)queryParam1)) // TODO: Change to call to ConvertToQueryParameter()
                .Append("Controller".PairWithValue((object)controllerName))
                .ToDictionary();
            // TODO: Check if query param has DefaultId attribute 

            return onParsed(controllerName, queryParameters);
        }
        
        public static Uri GetLocation<TController>(this UrlHelper url,
            Guid? idMaybe,
            string routeName = default(string))
        {
            if (idMaybe.HasValue)
                return url.GetLocation<TController>(idMaybe.Value, routeName);
            return default(Uri);
        }

        public static Uri GetLocation(this UrlHelper url, Type controllerType, Guid id,
            string routeName = "DefaultApi")
        {
            if (String.IsNullOrWhiteSpace(routeName))
            {
                    routeName = "DefaultApi";
            }

            var controllerName =
                controllerType.Name.TrimEnd("Controller",
                    (trimmedName) => trimmedName, (originalName) => originalName);
            var location = url.Link(routeName, new { Controller = controllerName, Id = id });
            return new Uri(location);
        }

        public static Uri GetLocation<TController>(this UrlHelper url,
            Guid id,
            string routeName = default(string))
        {
            return url.GetLocation(typeof(TController), id, routeName);
        }
        
        public static Uri GetLocation<TController>(this UrlHelper url,
            string action,
            string routeName = "DefaultApi")
        {
            var controllerName =
                typeof(TController).Name.TrimEnd("Controller",
                    (trimmedName) => trimmedName, (originalName) => originalName);
            var location = url.Link(routeName, new { Controller = controllerName, Action = action });
            return new Uri(location);
        }

        public static Uri GetLocationWithQuery(this UrlHelper url, Type controllerType, string query,
            string routeName = "DefaultApi")
        {
            var controllerName =
                controllerType.Name.TrimEnd("Controller",
                    (trimmedName) => trimmedName, (originalName) => originalName);
            var location = url.Link(routeName, new { Controller = controllerName });
            query = query.StartsWith($"?") ? query.Substring(1) : query;
            var uri = new UriBuilder(location) {Query = query};
            return uri.Uri;
        }

        public static Uri GetViewLocationWithActionId(this UrlHelper url, Type controllerType, string action, Guid id,
            string routeName = "Default")
        {
            var controllerName =
                controllerType.Name.TrimEnd("Controller",
                    (trimmedName) => trimmedName, (originalName) => originalName);
            var location = url.Link(routeName, new { Controller = controllerName, Action = action, id = id });
            return new Uri(location);
        }

        public static Uri GetLocationWithId(this UrlHelper url, Type controllerType, Guid id,
            string routeName = "DefaultApi")
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

            var location = url.Link(routeName, new { Controller = controllerName });
            location = location + "/" + id;
            return new Uri(location);
        }

        public static Uri GetLocationWithId(this UrlHelper url, Type controllerType, string action, Guid id,
            string routeName = "DefaultApi")
        {
            var controllerName =
                controllerType.Name.TrimEnd("Controller",
                    (trimmedName) => trimmedName, (originalName) => originalName);
            var location = url.Link(routeName, new { Controller = controllerName, Action = action });
            location = location + "/" + id;
            return new Uri(location);
        }

        public static Uri GetLocationWithIdAndQuery(this UrlHelper url, Type controllerType, Guid id, string query,
            string routeName = "DefaultApi")
        {
            var controllerName =
                controllerType.Name.TrimEnd("Controller",
                    (trimmedName) => trimmedName, (originalName) => originalName);
            var location = url.Link(routeName, new { Controller = controllerName });
            location = location + "/" + id;
            query = query.StartsWith($"?") ? query.Substring(1) : query;
            var uri = new UriBuilder(location) { Query = query };
            return uri.Uri;
        }

        public static Uri AddParameter(this Uri url, string paramName, string paramValue)
        {
            var uriBuilder = new UriBuilder(url);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query[paramName] = paramValue;
            uriBuilder.Query = query.ToString();

            return uriBuilder.Uri;
        }

        public static string ToStringOneCharacter(this DayOfWeek dayOfWeek)
        {
            var dtInfo = new System.Globalization.DateTimeFormatInfo();
            dtInfo.AbbreviatedDayNames = new string[] { "U", "M", "T", "W", "R", "F", "S" }; // MTWRFSU
            var dayOfWeekString = dtInfo.GetDayName(dayOfWeek);
            return dayOfWeekString;
        }

        public static TResult ToDayOfWeek<TResult>(this string oneCharacterDayOfWeekAsString,
            Func<DayOfWeek, TResult> success,
            Func<TResult> noMatch)
        {
            var mapping = new Dictionary<string, DayOfWeek>()
            {
                { "U", DayOfWeek.Sunday },
                { "M", DayOfWeek.Monday },
                { "T", DayOfWeek.Tuesday },
                { "W", DayOfWeek.Wednesday },
                { "R", DayOfWeek.Thursday },
                { "F", DayOfWeek.Friday },
                { "S", DayOfWeek.Saturday },
            };
            if (mapping.ContainsKey(oneCharacterDayOfWeekAsString.ToUpper()))
                return success(mapping[oneCharacterDayOfWeekAsString.ToUpper()]);
            DayOfWeek dayOfWeek;
            if (Enum.TryParse(oneCharacterDayOfWeekAsString, out dayOfWeek))
                return success(dayOfWeek);
            return noMatch();
        }

        public static DateTime? AsZulu(this DateTime? datetime)
        {
            return datetime.HasValue && (!datetime.Value.IsDefault()) ?
                    datetime.Value.ToUniversalTime()
                    :
                    default(DateTime?);
        }
        
    }
}
