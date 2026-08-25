using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;

using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Reflection;
using EastFive.Linq.Expressions;

namespace EastFive.Api
{
    public interface IProvideServerLocation
    {
        Uri ServerLocation { get; }
    }

    public interface IProvideHttpRequest
    {
        IHttpRequest HttpRequest { get; }
    }

    public interface IProvideRequestExpression<TResource>
    {
        IQueryable<TResource> FromExpression(Expression condition);
    }

    public static class ResourceQueryExtensions
    {
        [AttributeUsage(AttributeTargets.Method)]
        public class BinaryComparisonQueryAttribute : Attribute, IBuildHttpRequests, IBuildUrls
        {
            public Uri BindUrlQueryValue(Uri url, MethodInfo method, Expression[] arguments)
            {
                return method.TryParseMemberAssignment(arguments,
                    (memberInfo, type, content) =>
                    {
                        var queryParamName = GetParamName();
                        var queryParamValue = GetContent(type);
                        return url.AddQueryParameter(queryParamName, queryParamValue);

                        string GetParamName()
                        {
                            var apiBindings = memberInfo.GetAttributesInterface<IProvideApiValue>(true);
                            if (apiBindings.Any())
                            {
                                var apiBinding = apiBindings.First();
                                var propName = apiBinding.GetPropertyName(memberInfo);
                                if (propName.HasBlackSpace())
                                    return propName;
                            }
                            return memberInfo.Name;
                        }

                        string GetContent(ExpressionType expressionType)
                        {
                            if(expressionType == ExpressionType.Not)
                            {
                                if (content == null)
                                    return "not(null)";
                                var contentType = content.GetType();

                                if(contentType == typeof(bool))
                                {
                                    var boolContent = (bool)content;
                                    return (!boolContent).ToString();
                                }

                                if (contentType == typeof(string))
                                {
                                    var stringContent = content as string;
                                    if (stringContent.ToLower() == "value")
                                        return "null";
                                    if (stringContent.ToLower() == "not(null)")
                                        return "null";
                                    if (stringContent.ToLower() == "null")
                                        return "not(null)";
                                    return $"not({stringContent})";
                                }

                                var innerContent = GetContent(ExpressionType.Equal);
                                return $"not({innerContent})";
                            }

                            var memberType = memberInfo.GetMemberType();
                            if (memberType.IsNullable())
                            {
                                if (!content.NullableHasValue())
                                    return "null";
                                memberType = memberType.GetNullableUnderlyingType();
                                content = content.GetNullableValue();
                            }

                            if (typeof(string).IsAssignableFrom(memberType))
                            {
                                return (string)content;
                            }

                            if (typeof(Guid).IsAssignableFrom(memberType))
                            {
                                var contentId = (Guid)content;
                                return contentId.ToString();
                            }
                            if(memberType.IsSubClassOfGeneric(typeof(IReferenceable)))
                            {
                                if (content.IsDefaultOrNull())
                                    return string.Empty;
                                var contentType = content.GetType();
                                if (contentType.IsSubClassOfGeneric(typeof(IReferenceable)))
                                {
                                    var contentRef = (IReferenceable)content;
                                    return contentRef.id.ToString();
                                }
                            }
                            if (memberType.IsSubClassOfGeneric(typeof(IReferenceableOptional)))
                            {
                                if (content.IsDefaultOrNull())
                                    return "null";
                                var contentType = content.GetType();
                                if (contentType.IsSubClassOfGeneric(typeof(IReferenceableOptional)))
                                {
                                    var contentRefOptional = (IReferenceableOptional)content;
                                    if (!contentRefOptional.HasValue)
                                        return "empty";
                                    return contentRefOptional.id.Value.ToString();
                                }
                            }


                            if (typeof(DateTime).IsAssignableFrom(memberType))
                            {
                                var contentDate = (DateTime)content;
                                if(contentDate.Hour == 0 && contentDate.Minute == 0 && contentDate.Second == 0)
                                    return contentDate.ToString("yyyy-MM-dd");
                                return contentDate.ToString();
                            }

                            if (memberType.IsNumeric())
                            {
                                return content.ToString();
                            }

                            if (typeof(Type) == memberType)
                            {
                                return (content as Type).GetCustomAttributes<FunctionViewControllerAttribute>()
                                    .First().ContentType;
                            }

                            throw new NotImplementedException();
                        }
                    },
                    () =>
                    {
                        throw new Exception();
                    });
            }

            public IHttpRequest MutateRequest(IHttpRequest request,
                MethodInfo method, Expression[] arguments)
            {
                request.RequestUri = BindUrlQueryValue(request.RequestUri, method, arguments);
                return request;
            }
        }

        [AttributeUsage(AttributeTargets.Method)]
        public class MutateIdQueryAttribute : Attribute, IBuildHttpRequests, IBuildUrls
        {
            public Uri BindUrlQueryValue(Uri url, MethodInfo method, Expression[] arguments)
            {
                var queryParameters = arguments
                    .Zip(
                        method.GetParameters(),
                        (arg, paramInfo) => paramInfo.PairWithValue(arg))
                    .Select(argument => argument.Key.PairWithValue(argument.Value.Resolve()))
                    .ToArray();
                var value = queryParameters.First().Value;
                
                var idStr =
                    value.IsDefaultOrNull()?
                        "null"
                        :
                        typeof(Guid).IsAssignableFrom(value.GetType()) ?
                            ((Guid)value).ToString("N")
                            :
                            ((string)value);
                return url.AppendToPath(idStr);
            }

            public IHttpRequest MutateRequest(IHttpRequest request,
                MethodInfo method, Expression[] arguments)
            {
                request.RequestUri = BindUrlQueryValue(request.RequestUri, method, arguments);
                return request;
            }
        }

        private class Provider<TResource> : Linq.QueryProvider<
                EastFive.Linq.Queryable<TResource, Provider<TResource>>>
        {
            public Provider(Guid resourceId) : base(
                (func, type) => new Linq.Queryable<TResource, Provider<TResource>>(new Provider<TResource>(resourceId)), 
                (func, expression, type) =>
                {
                    var condition = Expression.Call(
                        typeof(ResourceQueryExtensions), "ById", new Type[] { typeof(TResource) },
                        expression, Expression.Constant(resourceId));
                    //var predicate = Expression.Lambda<Func<TResource, bool>>(condition, valueSelector.Parameters);
                    return new Linq.Queryable<TResource, Provider<TResource>>(
                        new Provider<TResource>(resourceId), expression);
                })
            {

            }

            public override object Execute(Expression expression)
            {
                throw new NotImplementedException();
            }
        }

        [MutateIdQuery]
        public static IQueryable<TResource> ById<TResource>(this IQueryable<TResource> query, Guid resourceId)
            where TResource : IReferenceable
        {
            if (!typeof(IProvideRequestExpression<TResource>).IsAssignableFrom(query.GetType()))
                throw new ArgumentException($"query must be of type `{typeof(IProvideRequestExpression<TResource>).FullName}` not `{query.GetType().FullName}`", "query");
            var requestMessageQuery = query as IProvideRequestExpression<TResource>;

            var condition = Expression.Call(
                typeof(ResourceQueryExtensions), "ById", new Type[] { typeof(TResource) },
                query.Expression, Expression.Constant(resourceId));

            var requestMessageNewQuery = requestMessageQuery.FromExpression(condition);
            return requestMessageNewQuery;
        }

        [MutateIdQuery]
        public static IQueryable<TResource> ById<TResource>(this IQueryable<TResource> query, IRef<TResource> resourceRef)
            where TResource : IReferenceable
        {
            if (!typeof(IProvideRequestExpression<TResource>).IsAssignableFrom(query.GetType()))
                throw new ArgumentException($"query must be of type `{typeof(IProvideRequestExpression<TResource>).FullName}` not `{query.GetType().FullName}`", "query");
            var requestMessageQuery = query as IProvideRequestExpression<TResource>;

            var condition = Expression.Call(
                typeof(ResourceQueryExtensions), "ById", new Type[] { typeof(TResource) },
                query.Expression, Expression.Constant(resourceRef.id));

            var requestMessageNewQuery = requestMessageQuery.FromExpression(condition);
            return requestMessageNewQuery;
        }

        [MutateIdQuery]
        public static IQueryable<TResource> ById<TResource>(this IQueryable<TResource> query, IRefOptional<TResource> resourceRef)
            where TResource : IReferenceable
        {
            if (!resourceRef.HasValue)
                return query;

            return query.ById(resourceRef.Ref);
        }

        [MutateIdQuery]
        public static IQueryable<TResource> ById<TResource>(this IQueryable<TResource> query, string resourceId)
        {
            if (!typeof(IProvideRequestExpression<TResource>).IsAssignableFrom(query.GetType()))
                throw new ArgumentException($"query must be of type `{typeof(IProvideRequestExpression<TResource>).FullName}` not `{query.GetType().FullName}`", "query");
            var requestMessageQuery = query as IProvideRequestExpression<TResource>;

            var method = typeof(ResourceQueryExtensions)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == nameof(ById))
                .Where(
                    m =>
                    {
                        var ps = m.GetParameters();
                        if (ps.Length < 2)
                            return false;
                        var p = ps[1];
                        if (p.Name != nameof(resourceId))
                            return false;

                        return p.ParameterType == typeof(string);
                    })
                .First()
                .MakeGenericMethod(new Type[] { typeof(TResource) });

            var condition = Expression.Call(method,
                query.Expression, Expression.Constant(resourceId, typeof(string)));

            var requestMessageNewQuery = requestMessageQuery.FromExpression(condition);
            return requestMessageNewQuery;
        }

        [AttributeUsage(AttributeTargets.Method)]
        public class QueryParamQueryAttribute : Attribute, IBuildHttpRequests, IBuildUrls
        {
            public Uri BindUrlQueryValue(Uri url, MethodInfo method, Expression[] arguments)
            {
                var key = (string)arguments[0].Resolve();
                var value = (string)arguments[1].Resolve();

                return url.AddQueryParameter(key, value);
            }

            public IHttpRequest MutateRequest(IHttpRequest request,
                MethodInfo method, Expression[] arguments)
            {
                request.RequestUri = BindUrlQueryValue(request.RequestUri, method, arguments);
                return request;
            }
        }

        [QueryParamQuery]
        public static IQueryable<TResource> QueryParam<TResource>(this IQueryable<TResource> query, string key, string value)
            where TResource : IReferenceable
        {
            if (!typeof(IProvideRequestExpression<TResource>).IsAssignableFrom(query.GetType()))
                throw new ArgumentException($"query must be of type `{typeof(IProvideRequestExpression<TResource>).FullName}` not `{query.GetType().FullName}`", "query");
            var requestMessageQuery = query as IProvideRequestExpression<TResource>;

            var methodInfo = typeof(ResourceQueryExtensions)
                .GetMethod(nameof(QueryParam), BindingFlags.Static | BindingFlags.Public)
                .MakeGenericMethod(typeof(TResource));
            var condition = Expression.Call(methodInfo, query.Expression, 
                Expression.Constant(key), Expression.Constant(value));

            var requestMessageNewQuery = requestMessageQuery.FromExpression(condition);
            return requestMessageNewQuery;
        }

        //[QueryParamQuery]
        //public static IQueryable<TResource> QueryParam<TResource>(this IQueryable<TResource> query, Expression<bool> paramExpr)
        //    where TResource : IReferenceable
        //{
        //    if (!typeof(RequestMessage<TResource>).IsAssignableFrom(query.GetType()))
        //        throw new ArgumentException($"query must be of type `{typeof(RequestMessage<TResource>).FullName}` not `{query.GetType().FullName}`", "query");
        //    var requestMessageQuery = query as RequestMessage<TResource>;

        //    var condition = Expression.Call(
        //        typeof(ResourceQueryExtensions), "QueryParam", new Type[] { typeof(TResource) },
        //        query.Expression, paramExpr);

        //    var requestMessageNewQuery = requestMessageQuery.FromExpression(condition);
        //    return requestMessageNewQuery;
        //}
    }
}
