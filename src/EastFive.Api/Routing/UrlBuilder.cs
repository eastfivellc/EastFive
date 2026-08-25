using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using EastFive.Collections.Generic;
using EastFive.Reflection;
using Microsoft.AspNetCore.Mvc.Routing;
using EastFive.Api.Core;

namespace EastFive.Api
{
    public class UrlBuilder : IBuildUrls, IProvideServerLocation
    {
        //private IProvideUrl urlHelper;
        //private IApplication httpApp;
        private Uri baseUrl;
        private IHttpRequest httpRequest;

        public UrlBuilder(IHttpRequest httpRequest) //, IApplication httpApp)
        {
            //this.httpApp = httpApp;
            //this.urlHelper = new CoreUrlProvider((httpRequest as CoreHttpRequest).request.HttpContext);
            var uriBuilder = new UriBuilder(httpRequest.RequestUri);
            uriBuilder.Path = String.Empty;
            uriBuilder.Query = String.Empty;
            this.baseUrl = uriBuilder.Uri;
            this.httpRequest = httpRequest;
        }

        public UrlBuilder(Uri baseUrl) //, IApplication httpApp)
        {
            this.baseUrl = baseUrl;
        }

        public IQueryable<T> Resources<T>()
        {
            var queryProvider = new RenderableQueryProvider(this);
            return new RenderableQuery<T>(queryProvider);
        }

        public Uri BindUrlQueryValue(Uri url, MethodInfo method, Expression[] arguments)
        {
            throw new NotImplementedException();
        }

        public Uri ServerLocation { get => baseUrl; }

        private class RenderableQueryProvider : IQueryProvider
        {
            public UrlBuilder builder;

            public RenderableQueryProvider(UrlBuilder builder)
            {
                this.builder = builder;
            }

            public IQueryable CreateQuery(Expression expression)
            {
                Type elementType = expression.Type.GetElementType();
                try
                {
                    return (IQueryable)Activator.CreateInstance(
                        typeof(RenderableQuery<>).MakeGenericType(elementType),
                        new object[] { this, expression });
                }
                catch (TargetInvocationException tie)
                {
                    throw tie.InnerException;
                }
            }

            public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
            {
                return new RenderableQuery<TElement>(this, expression);
            }

            public TResult Execute<TResult>(Expression expression)
            {
                return (TResult)this.Execute(expression);
            }

            private static IDictionary<string, string> Translate(Expression expression)
            {
                if (expression is MethodCallExpression)
                {
                    var methodCallExpression = expression as MethodCallExpression;
                    if (methodCallExpression.Method.DeclaringType == typeof(Queryable) && methodCallExpression.Method.Name == "Where")
                    {
                        //var queryParam = methodCallExpression 
                        //LambdaExpression lambda = (LambdaExpression)StripQuotes(m.Arguments[1]);
                        //this.Visit(lambda.Body);
                        //return m;
                    }
                }
                return null;
            }

            public object Execute(Expression expression)
            {
                throw new NotImplementedException();
            }
        }

        private class RenderableQuery<T> : IQueryable<T>,
            IProvideRequestExpression<T>,
            IProvideServerLocation,
            IProvideHttpRequest
            //, IRenderUrls
        {
            private RenderableQueryProvider provider;

            public RenderableQuery(RenderableQueryProvider provider)
            {
                this.provider = provider;
                this.Expression = Expression.Constant(this);
            }

            public RenderableQuery(RenderableQueryProvider provider, Expression expression)
            {
                this.provider = provider;

                if (expression == null)
                    throw new ArgumentNullException("expression");
                if (!typeof(IQueryable<T>).IsAssignableFrom(expression.Type))
                    throw new ArgumentOutOfRangeException("expression");
                this.Expression = expression;
            }

            public Expression Expression { get; }

            public Type ElementType => typeof(T);

            public IQueryProvider Provider => this.provider;

            public Uri ServerLocation => provider.builder.baseUrl;

            public IHttpRequest HttpRequest => provider.builder.httpRequest;

            public IQueryable<T> FromExpression(Expression condition)
            {
                return new RenderableQuery<T>(this.provider, condition);
            }

            public IEnumerator<T> GetEnumerator()
            {
                return ((IEnumerable<T>)this.provider.Execute(this.Expression)).GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return ((IEnumerable)this.provider.Execute(this.Expression)).GetEnumerator();
            }

            //public override string ToString()
            //{
            //    return this.RenderLocation().AbsoluteUri;
            //}

            //public Uri RenderLocation(string routeName = "DefaultApi")
            //{
            //    return this.provider.RenderLocation(this.Expression, routeName);
            //}
        }

        internal class QueryTranslator : ExpressionVisitor
        {
            List<KeyValuePair<string, string>> queryParams;
            IApplication application;

            internal QueryTranslator(IApplication application)
            {
                this.application = application;
            }

            internal IDictionary<string, string> Translate(Expression expression)
            {
                this.queryParams = new List<KeyValuePair<string, string>>();
                this.Visit(expression);
                return this.queryParams.ToDictionary();
            }

            private static Expression StripQuotes(Expression e)
            {
                while (e.NodeType == ExpressionType.Quote)
                {
                    e = ((UnaryExpression)e).Operand;
                }
                return e;
            }

            protected override Expression VisitMethodCall(MethodCallExpression m)
            {
                
                throw new NotSupportedException(string.Format("The method '{0}' is not supported", m.Method.Name));
            }

            protected override Expression VisitUnary(UnaryExpression u)
            {
                switch (u.NodeType)
                {
                    case ExpressionType.Not:
                        this.Visit(u.Operand);
                        break;
                    default:
                        throw new NotSupportedException(string.Format("The unary operator '{0}' is not supported", u.NodeType));
                }
                return u;
            }

            protected override Expression VisitBinary(BinaryExpression b)
            {
                this.Visit(b.Left);
                switch (b.NodeType)
                {
                    case ExpressionType.And:
                        break;
                    case ExpressionType.Or:
                        break;
                    case ExpressionType.Equal:
                        break;
                    case ExpressionType.NotEqual:
                        break;
                    case ExpressionType.LessThan:
                        break;
                    case ExpressionType.LessThanOrEqual:
                        break;
                    case ExpressionType.GreaterThan:
                        break;
                    case ExpressionType.GreaterThanOrEqual:
                        break;
                    default:
                        throw new NotSupportedException(string.Format("The binary operator '{0}' is not supported", b.NodeType));
                }
                this.Visit(b.Right);
                return b;
            }

            protected override Expression VisitConstant(ConstantExpression c)
            {
                IQueryable q = c.Value as IQueryable;
                if (q != null)
                {
                    // assume constant nodes w/ IQueryables are table references
                }
                else if (c.Value == null)
                {

                }
                else
                {
                    switch (Type.GetTypeCode(c.Value.GetType()))
                    {
                        case TypeCode.Boolean:
                            //sb.Append(((bool)c.Value) ? 1 : 0);
                            break;

                        case TypeCode.String:
                            //sb.Append(c.Value);
                            break;

                        case TypeCode.Object:
                            throw new NotSupportedException(string.Format("The constant for '{0}' is not supported", c.Value));

                        default:
                            //sb.Append(c.Value);
                            break;
                    }
                }
                return c;
            }

            protected override Expression VisitMember(MemberExpression m)
            {
                if (m.Expression != null && m.Expression.NodeType == ExpressionType.Parameter)
                {
                    return m;
                }
                return m;
            }
        }
    }

    public static class ProvideServerLocationExtensions
    {
        public static Uri UrlFor(this IProvideServerLocation location, Expression<Func<Task<IHttpResponse>>> method)
        {
            throw new NotImplementedException();
        }
    }
}
