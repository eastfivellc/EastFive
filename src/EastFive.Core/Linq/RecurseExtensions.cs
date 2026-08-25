using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Linq
{
    public static class RecurseExtensions
    {
        public static TResult Recurse<TItem, TResult>(this TItem item,
            Func<TItem, Func<TItem, TResult>, TResult> recurse)
        {
            return recurse(item,
                itemNext => itemNext.Recurse(recurse));
        }
    }
}
