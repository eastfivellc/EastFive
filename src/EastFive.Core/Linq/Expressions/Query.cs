using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using EastFive;
using EastFive.Extensions;
using EastFive.Collections.Generic;
using EastFive.Reflection;

namespace EastFive.Linq
{
    
    public class Query<TResource, TCarry>
        :
            EastFive.Linq.Queryable<
                TResource,
                Query<TResource, TCarry>.QueryProvideQuery>,
            IQueryable<TResource>
            // Linq.ISupplyQueryProvider<Query<TResource, TCarry>>
    {
        public TCarry carry;

        public Query(TCarry carry)
            : base(new QueryProvideQuery(out var getQueryCreator))
        {
            this.carry = carry;
            getQueryCreator(this);
        }

        protected Query(TCarry carry, Expression expr)
            : base(new QueryProvideQuery(out var getQueryCreator), expr)
        {
            this.carry = carry;
            getQueryCreator(this);
        }

        public class QueryProvideQuery : IQueryProvider
        {
            Query<TResource, TCarry> query;

            public QueryProvideQuery(out Action<Query<TResource, TCarry>> setQuery)
            {
                setQuery = (query) =>
                {
                    this.query = query;
                };
            }

            public IQueryable CreateQuery(Expression expression)
            {
                return query.From();
            }

            public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
            {
                return query.FromExpression(expression) as IQueryable<TElement>;
            }

            public TResult Execute<TResult>(Expression expression)
            {
                throw new NotImplementedException();
            }

            public object Execute(Expression expression)
            {
                throw new NotImplementedException();
            }
        }

        protected virtual Query<TResource, TCarry> FromExpression(Expression condition)
        {
            return new Query<TResource, TCarry>(this.carry, condition);
        }

        protected virtual Query<TResource, TCarry> From()
        {
            return new Query<TResource, TCarry>(this.carry);
        }
    }



}
