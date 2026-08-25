using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using EastFive.Extensions;

namespace EastFive
{
    public static class TypeExtensions
    {
        public static object ConvertObject(this Type type, object obj)
        {
            if (type == typeof(string))
                return obj.ToString();
            if (type == typeof(short))
                return Convert.ToInt16(obj);
            if (type == typeof(int))
                return Convert.ToInt32(obj);
            if (type == typeof(long))
                return Convert.ToInt64(obj);
            if (type == typeof(bool))
                return Convert.ToBoolean(obj);
            if (type == typeof(decimal))
                return Convert.ToDecimal(obj);
            if (type == typeof(double))
                return Convert.ToDouble(obj);
            if (type == typeof(float))
                return Convert.ToDecimal(obj);
            throw new ArgumentException("Unsupported conversion type:" + type.FullName);
        }

        public static object GetDefault(this Type t)
        {
            Func<object> f = GetDefault<object>;
            return f.Method.GetGenericMethodDefinition().MakeGenericMethod(t).Invoke(null, null);
        }

        private static T GetDefault<T>()
        {
            return default(T);
        }

        public static bool IsSubClassOfGeneric(this Type child, Type parent)
        {
            if (child == parent)
                return false;

            if (child.IsSubclassOf(parent))
                return true;

            var parameters = parent.GetGenericArguments();
            var isParameterLessGeneric = !(parameters != null && parameters.Length > 0 &&
                ((parameters[0].Attributes & TypeAttributes.BeforeFieldInit) == TypeAttributes.BeforeFieldInit));

            while (child != null && child != typeof(object))
            {
                var cur = GetFullTypeDefinition(child);
                if (parent == cur || (isParameterLessGeneric && cur.GetInterfaces().Select(i => GetFullTypeDefinition(i)).Contains(GetFullTypeDefinition(parent))))
                    return true;
                else if (!isParameterLessGeneric)
                    if (GetFullTypeDefinition(parent) == cur && !cur.IsInterface)
                    {
                        if (VerifyGenericArguments(GetFullTypeDefinition(parent), cur))
                            if (VerifyGenericArguments(parent, child))
                                return true;
                    }
                    else
                        foreach (var item in child.GetInterfaces().Where(i => GetFullTypeDefinition(parent) == GetFullTypeDefinition(i)))
                            if (VerifyGenericArguments(parent, item))
                                return true;

                child = child.BaseType;
            }

            return false;
        }

        private static Type GetFullTypeDefinition(Type type)
        {
            return type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        }

        private static bool VerifyGenericArguments(Type parent, Type child)
        {
            Type[] childArguments = child.GetGenericArguments();
            Type[] parentArguments = parent.GetGenericArguments();
            if (childArguments.Length == parentArguments.Length)
                for (int i = 0; i < childArguments.Length; i++)
                    if (childArguments[i].Assembly != parentArguments[i].Assembly || childArguments[i].Name != parentArguments[i].Name || childArguments[i].Namespace != parentArguments[i].Namespace)
                        if (!childArguments[i].IsSubclassOf(parentArguments[i]))
                            return false;

            return true;
        }

        public static bool IsNullable(this Type type)
        {
            // https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/nullable-types/how-to-identify-a-nullable-type
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        public static TResult IsNullable<TResult>(this Type type,
            Func<Type, TResult> onNullable,
            Func<TResult> onNotNullable)
        {
            if (!type.IsNullable())
                return onNotNullable();
            return onNullable(Nullable.GetUnderlyingType(type));
        }

        /// <summary>
        /// Cast a nullable value to it's non-nullable type, if it is not null.
        /// </summary>
        /// <exception cref="ArgumentException">If <paramref name="nullableValue"/> is not a nullable type</exception>
        /// <param name="nullableValue">Value that is of a nullable type.</param>
        /// <param name="nonNullValue">Value that has been cast from <paramref name="nullableValue"/>.</param>
        /// <returns></returns>
        public static bool TryGetNullableValue(this object nullableValue, out object nonNullValue)
        {
            if (nullableValue.IsDefaultOrNull())
            {
                nonNullValue = default;
                return false;
            }

            var nullableType = nullableValue.GetType();
            if (!nullableType.TryGetNullableUnderlyingType(out Type nonNullableType))
                throw new ArgumentException($"`{nullableType.FullName}` is not nullable.");

            nonNullValue = Convert.ChangeType(nullableValue, nonNullableType);
            return true;
        }

        public static bool TryGetNullableUnderlyingType(this Type type, out Type nonNullableType)
        {
            if (type.IsNullable())
            {
                nonNullableType = Nullable.GetUnderlyingType(type);
                return true;
            }

            nonNullableType = default;
            return false;
        }

        public static bool TryGetValue<T>(this T? nullableValue, out T value) where T : struct
        {
            if (nullableValue.HasValue)
            {
                value = nullableValue.Value;
                return true;
            }
            value = default;
            return false;
        }

        public static bool IsTuple(this Type type)
        {
            if (type.IsNull())
                return false;

            if (!typeof(TypeInfo).IsAssignableFrom(type.GetType()))
                return false;

            var valueType = (TypeInfo)type;
            var isTuple = valueType.ImplementedInterfaces
                .Contains(typeof(System.Runtime.CompilerServices.ITuple));

            return isTuple;
        }

        public static bool IsTuple(this Type type, out (Type type, string name)[] properties)
        {
            properties = new (Type type, string name)[] { };
            if (type.IsNull())
                return false;

            if (!typeof(TypeInfo).IsAssignableFrom(type.GetType()))
                return false;

            var valueType = (TypeInfo)type;
            var isTuple = valueType.ImplementedInterfaces
                .Contains(typeof(System.Runtime.CompilerServices.ITuple));

            if(isTuple)
            {
                properties = valueType.DeclaredFields
                    .Select(
                        declaredField =>
                        {
                            return (declaredField.FieldType, declaredField.Name);
                        })
                    .ToArray();
            }

            return isTuple;
        }

        public static bool IsTuple(this Type type, object value, out (Type type, string name, object value)[] properties)
        {
            properties = new (Type type, string name, object value)[] { };
            if (type.IsNull())
                return false;

            if (!typeof(TypeInfo).IsAssignableFrom(type.GetType()))
                return false;

            var valueType = (TypeInfo)type;
            var isTuple = valueType.ImplementedInterfaces
                .Contains(typeof(System.Runtime.CompilerServices.ITuple));

            if (isTuple)
            {
                properties = valueType.DeclaredFields
                    .Select(
                        declaredField =>
                        {
                            var v = declaredField.GetValue(value);
                            return (declaredField.FieldType, declaredField.Name, v);
                        })
                    .ToArray();
            }

            return isTuple;
        }

        public static Type GetNullableUnderlyingType(this Type type)
        {
            return Nullable.GetUnderlyingType(type);
        }

        public static Type GetNullValueForNullableType(this Type type)
        {
            return null;
        }

        public static bool IsNumeric(this Type type)
        {
            if (type.IsWholeNumber())
                return true;
            if (type.IsDecimalNumber())
                return true;
            return false;
        }

        public static bool IsWholeNumber(this Type type)
        {
            if (type == typeof(int))
                return true;
            if (type == typeof(long))
                return true;
            if (type == typeof(short))
                return true;
            if (type == typeof(byte))
                return true;
            if (type == typeof(uint))
                return true;
            if (type == typeof(ulong))
                return true;
            if (type == typeof(ushort))
                return true;
            if (type == typeof(sbyte))
                return true;
            return false;
        }

        public static bool IsDecimalNumber(this Type type)
        {
            if (type == typeof(float))
                return true;
            if (type == typeof(double))
                return true;
            if (type == typeof(decimal))
                return true;
            return false;
        }

        public static Type MakeArrayType(this Type t)
        {
            var discardArray = Array.CreateInstance(t, 0);
            var arrayType = discardArray.GetType();
            return arrayType;
        }

        public static string DisplayName(this Type type)
        {
            return type.Name;
        }

        public static string DisplayFullName(this Type type)
        {
            if(type.IsGenericType)
            {
                var genericNameWithTick = type.GetGenericTypeDefinition().FullName;
                var genericName = genericNameWithTick
                    .Substring(0, genericNameWithTick.IndexOf('`'));
                var typeNames = type.GenericTypeArguments
                    .Select(genType => genType.DisplayFullName())
                    .Join(',');
                return $"{genericName}<{typeNames}>";
            }
            return type.FullName;
        }
    }
}
