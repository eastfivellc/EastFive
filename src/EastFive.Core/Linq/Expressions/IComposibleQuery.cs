using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Linq.Expressions
{
    public interface IComposibleQuery<T>
    {
        IQueryable<T> FromExpression(Expression condition);
    }
}
