using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace EastFive.Extensions
{
    public static class MemberInfoExtensions
    {
        /// <summary>
        /// Given a lambda expression that calls a method, returns the method info.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <returns></returns>
        public static bool TryParseMemberComparison<TResource, TMember>(
                this Expression<Func<TResource, TMember>> expression, out MemberInfo member)
        {
            if (expression.Body is MemberExpression)
            {
                var memberExpr = expression.Body as MemberExpression;
                member = memberExpr.Member;
                return true;
            }
            member = default;
            return false;
        }

        public static string Identification(this MemberInfo memberInfo)
        {
            return $"{memberInfo.DeclaringType.FullName}..{memberInfo.Name}";
        }
    }
}