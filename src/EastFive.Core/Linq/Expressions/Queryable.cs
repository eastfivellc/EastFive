using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Linq
{
    public class Queryable<T, TQueryProvider> : IQueryable<T>
        where TQueryProvider : IQueryProvider
    {
        private TQueryProvider provider;

        public Queryable(TQueryProvider provider)
        {
            this.provider = provider;
            this.Expression = Expression.Constant(this);
        }

        public Queryable(TQueryProvider provider, Expression expression)
        {
            this.provider = provider;

            if (expression is null)
                throw new ArgumentNullException(nameof(expression));

            // This isn't valid for .Where clauses
            // if (!typeof(IQueryable<T>).IsAssignableFrom(expression.Type))
            //     throw new ArgumentOutOfRangeException("expression");
            
            this.Expression = expression;
        }

        public Expression Expression { get; }

        public Type ElementType => typeof(T);

        public IQueryProvider Provider => this.provider;

        public IEnumerator<T> GetEnumerator()
        {
            return ((IEnumerable<T>)this.provider.Execute(this.Expression)).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)this.provider.Execute(this.Expression)).GetEnumerator();
        }

    }
}
