using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using EastFive.Extensions;
using EastFive.Linq.Expressions;

namespace EastFive.Reflection
{
    public static class ExpressionExtensions
    {
        public static object ResolveExpression(this Expression expression)
        {
            if (expression is MemberExpression)
            {
                return GetValue((MemberExpression)expression);
            }

            if (expression is UnaryExpression)
            {
                // if casting is involved, Expression is not x => x.FieldName but x => Convert(x.Fieldname)
                return GetValue((MemberExpression)((UnaryExpression)expression).Operand);
            }

            if (expression is ParameterExpression)
            {
                // if casting is involved, Expression is not x => x.FieldName but x => Convert(x.Fieldname)
                var value = Expression.Lambda(expression as ParameterExpression).Compile().DynamicInvoke();
                return value;
            }

            if (expression is LambdaExpression)
            {
                var lambdaExpression = expression as System.Linq.Expressions.LambdaExpression;
                return null;
            }

            if (expression is ConstantExpression)
            {
                var constExpression = expression as ConstantExpression;
                return constExpression.Value;
            }

            try
            {
                var value = Expression.Lambda(expression).Compile().DynamicInvoke();
                return value;
            }
            catch (Exception ex)
            {
                throw new Exception($"Exception while attempting to execute expression:{expression.ToString()}", ex);
            }
        }

        public static object GetValue(this MemberExpression exp)
        {
            // expression is ConstantExpression or FieldExpression
            if (exp.Expression is ConstantExpression)
            {
                return (((ConstantExpression)exp.Expression).Value)
                        .GetType()
                        .GetField(exp.Member.Name)
                        .GetValue(((ConstantExpression)exp.Expression).Value);
            }

            if (exp.Expression is MemberExpression)
            {
                return GetValue((MemberExpression)exp.Expression);
            }

            if (exp.Expression is MethodCallExpression)
            {
                try
                {
                    var value = Expression.Lambda(exp.Expression as MethodCallExpression).Compile().DynamicInvoke();
                    return value;
                }
                catch (Exception ex)
                {
                    ex.GetType();
                }
            }

            throw new NotImplementedException();
        }

        public static MemberInfo MemberComparison(this Expression expression, out ExpressionType relationship, out object value)
        {
            var result = expression.MemberComparison(
                (memberInfo, r, v) =>
                    new
                    {
                        memberInfo,
                        r,
                        v,
                    },
                () => throw new NotImplementedException());
            relationship = result.r;
            value = result.v;
            return result.memberInfo;
        }

        public static TResult MemberComparison<TResult>(this Expression expression,
            Func<MemberInfo, ExpressionType, object, TResult> onResolved,
            Func<TResult> onNotResolved)
        {
            if (expression is UnaryExpression)
            {
                var argUnary = expression as UnaryExpression;
                return MemberComparison(argUnary.Operand,
                    onResolved,
                    onNotResolved);
            }
            if (expression is BinaryExpression binaryExpr)
            {
                var relationship = binaryExpr.NodeType;
                if (TryGetMemberExpression(binaryExpr.Left, out var memberInfoLeft))
                {
                    var value = binaryExpr.Right.Resolve();
                    return onResolved(memberInfoLeft, relationship, value);
                }
                if (TryGetMemberExpression(binaryExpr.Right, out var memberInfoRight))
                {
                    var value = binaryExpr.Left.Resolve();
                    return onResolved(memberInfoRight, relationship, value);
                }

                bool TryGetMemberExpression(Expression expr, out MemberInfo memberInfo)
                {
                    if (expr is MemberExpression)
                    {
                        var memberExpr = expr as MemberExpression;
                        if (memberExpr.Expression.NodeType == ExpressionType.Parameter)
                        {
                            memberInfo = memberExpr.Member;
                            return true;
                        }
                    }
                    if (expr.NodeType == ExpressionType.Convert)
                    {
                        var unaryExpr = expr as UnaryExpression;
                        return TryGetMemberExpression(unaryExpr.Operand, out memberInfo);
                    }
                    memberInfo = default;
                    return false;
                }
            }
            if(expression is LambdaExpression)
            {
                var paramExpr = expression as LambdaExpression;
                return MemberComparison(paramExpr.Body,
                    onResolved,
                    onNotResolved);
            }
            if(expression is ConstantExpression)
            {
                var constExpr = expression as ConstantExpression;
                if(constExpr.Type == typeof(Boolean))
                {
                    if((bool)constExpr.Value)
                    {
                        return onResolved(null, ExpressionType.IsTrue, true);
                    }
                }
            }

            return onNotResolved();
        }

        public static bool TryGetMemberExpression(this Expression expression, out MemberInfo memberInfo)
        {
            if (expression is UnaryExpression)
            {
                var argUnary = expression as UnaryExpression;
                return TryGetMemberExpression(argUnary.Operand, out memberInfo);
            }
            if (expression is BinaryExpression)
            {
                var binaryExpr = expression as BinaryExpression;
                if (TryGetMemberExpression(binaryExpr.Left, out memberInfo))
                    return true;
                if (TryGetMemberExpression(binaryExpr.Right, out memberInfo))
                    return true;
            }
            if (expression is LambdaExpression)
            {
                var paramExpr = expression as LambdaExpression;
                return TryGetMemberExpression(paramExpr.Body, out memberInfo);
            }
            if (expression is MemberExpression)
            {
                var memberExpression = expression as MemberExpression;
                memberInfo = memberExpression.Member;
                return true;
            }
            memberInfo = default;
            return false;
        }

        public static TResult PropertyName<TObject, TProperty, TResult>(this Expression<Func<TObject, TProperty>> propertyExpression,
            Func<string, TResult> onPropertyExpression,
            Func<TResult> onNotPropertyExpression)
        {
            return propertyExpression.PropertyInfo(
                propertyInfo => onPropertyExpression(propertyInfo.Name),
                onNotPropertyExpression);
        }

        public static TResult PropertyInfo<TObject, TProperty, TResult>(this Expression<Func<TObject, TProperty>> propertyExpression,
            Func<PropertyInfo, TResult> onPropertyExpression,
            Func<TResult> onNotPropertyExpression)
        {
            return propertyExpression.MemberInfo(
                (memberInfo) =>
                {
                    if (memberInfo is PropertyInfo)
                    {
                        var propertyInfo = memberInfo as PropertyInfo;
                        return onPropertyExpression(propertyInfo);
                    }
                    return onNotPropertyExpression();
                },
                onNotPropertyExpression);
        }

        public static TResult FieldInfo<TObject, TProperty, TResult>(this Expression<Func<TObject, TProperty>> fieldExpression,
            Func<FieldInfo, TResult> onPropertyExpression,
            Func<TResult> onNotPropertyExpression)
        {
            return fieldExpression.MemberInfo(
                (memberInfo) =>
                {
                    if (memberInfo is FieldInfo)
                    {
                        var propertyInfo = memberInfo as FieldInfo;
                        return onPropertyExpression(propertyInfo);
                    }
                    return onNotPropertyExpression();
                },
                onNotPropertyExpression);
        }

        public static TResult MemberInfo<TObject, TProperty, TResult>(this Expression<Func<TObject, TProperty>> propertyExpression,
            Func<MemberInfo, TResult> onPropertyExpression,
            Func<TResult> onNotPropertyExpression)
        {
            if (propertyExpression.Body.IsDefault())
                return onNotPropertyExpression();

            if (!(propertyExpression.Body is MemberExpression))
                return onNotPropertyExpression();

            var memberExpression = propertyExpression.Body as MemberExpression;
            var memberInfo = memberExpression.Member;
            return onPropertyExpression(memberInfo);
        }

        public static TResult MemberInfo<TObject, TProperty, TResult>(this Expression<Func<TObject, TProperty>> propertyExpression,
            Func<MemberInfo, Expression, TResult> onPropertyExpression,
            Func<TResult> onNotPropertyExpression)
        {
            if (propertyExpression.Body.IsDefault())
                return onNotPropertyExpression();

            if (!(propertyExpression.Body is MemberExpression))
                return onNotPropertyExpression();

            var memberExpression = propertyExpression.Body as MemberExpression;
            var memberInfo = memberExpression.Member;
            var expression = memberExpression.Expression;
            return onPropertyExpression(memberInfo, expression);
        }

        public static TResult MemberName<TObject, TProperty, TResult>(this Expression<Func<TObject, TProperty>> memberExpression,
            Func<string, TResult> onMemberExpression,
            Func<TResult> onNotMemberExpression)
        {
            if (memberExpression.Body.IsDefault())
                return onNotMemberExpression();

            if (!(memberExpression.Body is MemberExpression))
                return onNotMemberExpression();

            var memberExpressionTyped = memberExpression.Body as MemberExpression;
            var member = memberExpressionTyped.Member;
            return onMemberExpression(member.Name);
        }

        public static object GetValue(this MemberInfo memberInfo, object obj)
        {
            return memberInfo.GetPropertyOrFieldValue(obj);
            
            //switch (memberInfo.MemberType)
            //{
            //    case MemberTypes.Field:
            //        return ((FieldInfo)memberInfo).GetValue(obj);
            //    case MemberTypes.Property:
            //        return ((PropertyInfo)memberInfo).GetValue(obj);
            //    default:
            //        throw new ArgumentException($"memberInfo of type '{memberInfo.MemberType}' which is unsupported");
            //}
        }

        public static void SetValue<T>(this MemberInfo memberInfo, ref T obj, object value)
        {
            // unbox in case of struct
            object objUnboxed = obj;
            switch (memberInfo.MemberType)
            {
                case MemberTypes.Field:
                    ((FieldInfo)memberInfo).SetValue(objUnboxed, value);
                    break;
                case MemberTypes.Property:
                    ((PropertyInfo)memberInfo).SetValue(objUnboxed, value);
                    break;
                default:
                    throw new ArgumentException($"memberInfo of type '{memberInfo.MemberType}' which is unsupported");
            }
            obj = (T)objUnboxed;
        }

        public static Type GetMemberType(this MemberInfo memberInfo)
        {
            switch (memberInfo.MemberType)
            {
                case MemberTypes.Field:
                    return ((FieldInfo)memberInfo).FieldType;
                case MemberTypes.Property:
                    return ((PropertyInfo)memberInfo).PropertyType;
                default:
                    throw new ArgumentException($"memberInfo of type '{memberInfo.MemberType}' which is unsupported");
            }
        }


        public static KeyValuePair<ParameterInfo, object>[] ResolveArgs(this MethodCallExpression body)
        {
            var values = body.Arguments
                .Zip(
                    body.Method.GetParameters(),
                    (arg, paramInfo) => paramInfo.PairWithValue(arg))
                .Select(argument => argument.Key.PairWithValue(Resolve(argument.Value)))
                .ToArray();
            return values;
        }

        /// <summary>
        /// Attempt to resolve the value of the expression without compiling and invoking the expression.
        /// </summary>
        /// <remarks>
        /// This method will compile and invoke the expression,
        /// or a subset of the expression, 
        /// if it fails to resolve it with other means.
        /// Methods called by the expression will be invoked.
        /// </remarks>
        /// <param name="expression"></param>
        /// <returns></returns>
        public static object Resolve(this Expression expression)
        {
            if (expression is MemberExpression)
            {
                var memberExpression = expression as MemberExpression;
                if (!memberExpression.Expression.IsDefaultOrNull())
                {
                    var memberObject = memberExpression.Expression.Resolve();
                    //var memberOfObjectAccessed = memberObject.GetType().GetField(memberExpression.Member.Name);
                    //var memberValue = memberOfObjectAccessed.GetValue(memberObject);
                    var memberValue = memberExpression.Member.GetValue(memberObject);
                    return memberValue;
                }
            }

            if (expression is UnaryExpression)
            {
                var unaryExpression = expression as UnaryExpression;

                // ensure that there is an operandValue
                if (!unaryExpression.Operand.IsDefaultOrNull())
                {
                    var operandValue = unaryExpression.Operand.Resolve();

                    // if casting is involved, Expression is not value but Convert(value)
                    // or possibly some other method than convert, either way, just get the method
                    var unaryMethod = unaryExpression.Method;

                    // if the method is null, just return the operandValue
                    if (unaryMethod.IsDefaultOrNull())
                        return operandValue;

                    // and ensure the method is static and only has one parameter
                    if (unaryMethod.IsStatic && unaryMethod.GetParameters().Length == 1)
                    {
                        try
                        {
                            var convertedValue = unaryExpression.Method.Invoke(null, new object[] { operandValue });
                            return convertedValue;
                        }
                        catch (Exception ex)
                        {
                            ex.GetType();
                        }
                    }
                    // otherwise more work is required to call it.
                }
                // otherwise more work is required to call it.
            }

            if (expression is ConstantExpression)
            {
                var constantExpression = expression as ConstantExpression;
                var constantValue = constantExpression.Value;
                return constantValue;
            }

            var value = Expression.Lambda(expression).Compile().DynamicInvoke();
            return value;
        }

        public static TResult TryParseMemberAssignment<TResult>(this MethodInfo method,
                Expression[] arguments,
            Func<MemberInfo, ExpressionType, object, TResult> onBinaryAssignment,
            Func<TResult> onCouldNotParse)
        {
            foreach (var argument in arguments)
            {
                if (argument is System.Linq.Expressions.UnaryExpression)
                {
                    var unaryExpr = argument as System.Linq.Expressions.UnaryExpression;
                    return TryParseMemberAssignment(unaryExpr.Method, unaryExpr.Operand.AsArray(),
                        onBinaryAssignment,
                        onCouldNotParse);
                }
                if (argument is System.Linq.Expressions.LambdaExpression)
                {
                    var lambdaExpr = (argument as System.Linq.Expressions.LambdaExpression);
                    if (lambdaExpr.Body is BinaryExpression)
                    {
                        var binaryExpr = lambdaExpr.Body as BinaryExpression;
                        var leftExpr = binaryExpr.Left;
                        var rightExpr = binaryExpr.Right;
                        if (leftExpr is MemberExpression)
                            return OnMember(leftExpr as MemberExpression);

                        // Catch Nullable casting
                        if (leftExpr is UnaryExpression)
                        {
                            var unaryExpr = leftExpr as UnaryExpression;
                            if (unaryExpr.Operand is MemberExpression)
                                return OnMember(unaryExpr.Operand as MemberExpression);
                        }

                        TResult OnMember(MemberExpression leftMemberExpr)
                        {
                            var member = leftMemberExpr.Member;
                            var value = rightExpr.Resolve();
                            return onBinaryAssignment(member, binaryExpr.NodeType, value);
                        }

                        return onCouldNotParse();
                    }
                    if (lambdaExpr.Body is UnaryExpression)
                    {
                        var unaryExpr = lambdaExpr.Body as UnaryExpression;
                        var nodeType = unaryExpr.NodeType;
                        return TryParseMemberAssignment(unaryExpr.Method, unaryExpr.Operand.AsArray(),
                            (memberInfo, type, value) => onBinaryAssignment(memberInfo, nodeType, value),
                            onCouldNotParse);
                    }
                    return onCouldNotParse();
                }
                if (argument is MemberExpression)
                {
                    var memberExp = argument as MemberExpression;
                    if (memberExp.Member.Name == "HasValue")
                    {
                        return TryParseMemberAssignment(method, memberExp.Expression.AsArray(),
                            (memberInfo, type, v) => onBinaryAssignment(memberInfo, ExpressionType.Equal, "value"),
                            onCouldNotParse);
                    }
                    object GetValue()
                    {
                        if (memberExp.Member.GetMemberType() == typeof(bool))
                            return true;
                        if (memberExp.Member.GetMemberType().IsSubClassOfGeneric(typeof(Nullable<>)))
                            return null;
                        return memberExp.Resolve();
                    }
                    var value = GetValue();
                    return onBinaryAssignment(memberExp.Member, memberExp.NodeType, value);
                }
            }
            return onCouldNotParse();
        }
    }
}
