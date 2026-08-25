using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace EastFive.Extensions
{
    public static class MethodInfoExtensions
    {
        /// <summary>
        /// Given a lambda expression that calls a method, returns the method info.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="expression">The expression.</param>
        /// <example>MethodInfoExtensions.GetMethodInfo<ExampleClass>(c => c.AMethod());</example>
        /// <returns></returns>
        public static MethodInfo GetMethodInfo(this Expression<Action> expression)
        {
            return GetMethodInfo((LambdaExpression)expression);
        }

        /// <summary>
        /// Given a lambda expression that calls a method, returns the method info.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="expression">The expression.</param>
        /// <example>
        /// MethodInfoExtensions.GetMethodInfo<ExampleClass>(c => c.AGenericMethod(default(int)));
        /// MethodInfoExtensions.GetMethodInfo(() => StaticExampleClass.StaticExampleMethod());
        /// </example>
        /// <returns></returns>
        public static MethodInfo GetMethodInfoGeneric<T>(this Expression<Action<T>> expression)
        {
            return GetMethodInfo((LambdaExpression)expression);
        }

        /// <summary>
        /// Given a lambda expression that calls a method, returns the method info.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="expression">The expression.</param>
        /// <returns></returns>
        public static MethodInfo GetMethodInfoFunc<T, TResult>(this Expression<Func<T, TResult>> expression)
        {
            return GetMethodInfo((LambdaExpression)expression);
        }

        /// <summary>
        /// Given a lambda expression that calls a method, returns the method info.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <returns></returns>
        public static MethodInfo GetMethodInfo(LambdaExpression expression)
        {
            MethodCallExpression outermostExpression = expression.Body as MethodCallExpression;

            if (outermostExpression is null)
            {
                throw new ArgumentException("Invalid Expression. Expression should consist of a Method call only.");
            }

            return outermostExpression.Method;
        }
    }
}
