using EastFive.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Linq
{
    public interface ISupplyQueryProvider<TQueryable>
        where TQueryable : IQueryable
    {
        TQueryable ActivateQueryable(QueryProvider<TQueryable> provider, Type type);

        TQueryable ActivateQueryableWithExpression(QueryProvider<TQueryable> queryProvider,
            Expression expression, Type elementType);
    }

    public abstract class QueryProvider<TQueryable> : IQueryProvider
        where TQueryable : IQueryable
    {
        public delegate TQueryable ActivateQueryableWithExpressionDelegate(QueryProvider<TQueryable> queryProvider,
            Expression expression, Type elementType);

        private Func<QueryProvider<TQueryable>, Type, TQueryable> activateQueryable;
        private ActivateQueryableWithExpressionDelegate activateQueryableWithExpression;

        public QueryProvider(Func<QueryProvider<TQueryable>, Type, TQueryable> activateQueryable,
            ActivateQueryableWithExpressionDelegate activateQueryableWithExpression)
        {
            this.activateQueryable = activateQueryable;
            this.activateQueryableWithExpression = activateQueryableWithExpression;
        }

        private ISupplyQueryProvider<TQueryable> supplyQueryProvider;

        public QueryProvider(ISupplyQueryProvider<TQueryable> supplyQueryProvider)
        {
            this.supplyQueryProvider = supplyQueryProvider;
        }

        public IQueryable CreateQuery(Expression expression)
        {
            var elementType = expression.Type.GetElementType();
            try
            {
                if (!supplyQueryProvider.IsDefaultOrNull())
                    return supplyQueryProvider.ActivateQueryable(this, elementType);
                return activateQueryable(this, elementType);
            }
            catch (TargetInvocationException tie)
            {
                throw tie.InnerException;
            }
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            if (!supplyQueryProvider.IsDefaultOrNull())
            {
                var queryable = supplyQueryProvider.ActivateQueryableWithExpression(this, expression, typeof(TElement));
                return queryable as IQueryable<TElement>;
            }
            {
                var queryable = this.activateQueryableWithExpression(this, expression, typeof(TElement));
                return queryable as IQueryable<TElement>;
            }
        }

        public TResult Execute<TResult>(Expression expression)
        {
            return (TResult)this.Execute(expression);
        }

        public abstract object Execute(Expression expression);
    }
}
