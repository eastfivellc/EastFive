using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive
{
    public static class ConditionalExtensions
    {
        public static TResult IfElse<TResult>(this bool v, Func<TResult> ifFunc, Func<TResult> elseFunc)
        {
            if (v)
                return ifFunc();
            return elseFunc();
        }

        public static TResult IfElseThen<T1, TResult>(this bool v,
            Func<Func<T1, TResult>, TResult> ifFunc, 
            Func<Func<T1, TResult>, TResult> elseFunc,
            Func<T1, TResult> thenFunc)
        {
            if (v)
                return ifFunc(thenFunc);
            return elseFunc(thenFunc);
        }
    }
}
