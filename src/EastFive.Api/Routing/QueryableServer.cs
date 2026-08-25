using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

using EastFive.Collections.Generic;
using EastFive.Linq;
using EastFive.Reflection;
using Microsoft.AspNetCore.Http;
using EastFive.Extensions;

namespace EastFive.Api
{
    [RequestMessage]
    public class QueryableServer<TResource>
        :
            EastFive.Linq.Queryable<
                TResource,
                QueryableServer<TResource>.QueryableServerProvideQuery>,
            IQueryable<TResource>,
            Linq.ISupplyQueryProvider<QueryableServer<TResource>>,
            IProvideServerLocation,
            IProvideRequestExpression<TResource>
    {
        public QueryableServer(IProvideServerLocation invokeApplication)
            : base(new QueryableServerProvideQuery(invokeApplication))
        {
            this.InvokeApplication = invokeApplication;
        }

        private QueryableServer(IProvideServerLocation invokeApplication, Expression expr)
            : base(new QueryableServerProvideQuery(invokeApplication), expr)
        {
            this.InvokeApplication = invokeApplication;
        }

        public IProvideServerLocation InvokeApplication { get; private set; }

        public QueryableServer<TRelatedResource> Related<TRelatedResource>()
        {
            return new QueryableServer<TRelatedResource>(this.InvokeApplication);
        }

        public Uri ServerLocation
        {
            get
            {
                var requestMessage = this;
                var uriString = requestMessage.InvokeApplication.ServerLocation.AbsoluteUri
                    .TrimEnd('/'.AsArray());
                return new Uri(uriString);
            }
        }

        public class QueryableServerProvideQuery :
            EastFive.Linq.QueryProvider<
                EastFive.Linq.Queryable<TResource,
                    EastFive.Api.QueryableServer<TResource>.QueryableServerProvideQuery>>
        {
            public QueryableServerProvideQuery(IProvideServerLocation invokeApplication)
                : base(
                    (queryProvider, type) => (queryProvider is QueryableServer<TResource>) ?
                        (queryProvider as QueryableServer<TResource>).From()
                        :
                        new QueryableServer<TResource>(invokeApplication),
                    (queryProvider, expression, type) => (queryProvider is QueryableServer<TResource>) ?
                        (queryProvider as QueryableServer<TResource>).FromExpression(expression)
                        :
                        new QueryableServer<TResource>(invokeApplication, expression))
            {
            }

            public override object Execute(Expression expression)
            {
                return 1;
            }
        }

        internal virtual QueryableServer<TResource> FromExpression(Expression condition)
        {
            return new QueryableServer<TResource>(
                  this.InvokeApplication,
                  condition);
        }

        internal virtual QueryableServer<TResource> From()
        {
            return new QueryableServer<TResource>(
                  this.InvokeApplication);
        }

        public QueryableServer<TResource> ActivateQueryable(QueryProvider<QueryableServer<TResource>> provider, Type type)
        {
            return From();
        }

        public QueryableServer<TResource> ActivateQueryableWithExpression(QueryProvider<QueryableServer<TResource>> queryProvider,
            Expression expression, Type elementType)
        {
            return FromExpression(expression);
        }

        IQueryable<TResource> IProvideRequestExpression<TResource>.FromExpression(Expression condition)
            => FromExpression(condition);
    }



}
