using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive
{
    public static class TResultExtensions
    {
        [DebuggerStepThrough]
        [DebuggerNonUserCode]
        [DebuggerStepperBoundary]
        public static Func<TResult> AsFunctionException<TResult>(this string message)
        {
            return () =>
            {
                throw new Exception(message);
            };
        }


        [DebuggerStepThrough]
        [DebuggerNonUserCode]
        [DebuggerStepperBoundary]
        public static Func<T1, TResult> AsFunctionException<T1, TResult>(this string message)
        {
            return (t1) =>
            {
                throw new Exception(message);
            };
        }
    }
}
