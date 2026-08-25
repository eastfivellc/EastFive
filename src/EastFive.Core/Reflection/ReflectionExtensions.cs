using EastFive.Extensions;
using EastFive.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Reflection
{
    public static class ReflectionExtensions
    {
        public static IEnumerable<MemberInfo> GetPropertyOrFieldMembers(this Type type)
        {
            return type
                .GetMembers()
                .Where(
                    member =>
                    {
                        if (member.MemberType == MemberTypes.Property)
                            return true;
                        if (member.MemberType == MemberTypes.Field)
                            return true;
                        return false;
                    });
        }

        public static IEnumerable<MemberInfo> GetStaticPropertyOrFieldMembers(this Type type)
        {
            return type
                .GetMembers(BindingFlags.Static | BindingFlags.Public)
                .Where(
                    member =>
                    {
                        if (member.MemberType == MemberTypes.Property)
                            return true;
                        if (member.MemberType == MemberTypes.Field)
                            return true;
                        return false;
                    });
        }

        public static Type GetPropertyOrFieldType(this MemberInfo memberType)
        {
            if (memberType is PropertyInfo)
                return (memberType as PropertyInfo).PropertyType;

            if (memberType is FieldInfo)
                return (memberType as FieldInfo).FieldType;

            if (memberType is Type)
                return (memberType as Type).UnderlyingSystemType;

            throw new ArgumentException("memberType",
                $"Cannot determine type of {memberType.GetType().FullName} since it could not be casted to {typeof(PropertyInfo).Name} or {typeof(FieldInfo).Name}.");
        }

        public static object GetPropertyOrFieldValue(this MemberInfo memberInfo, object obj)
        {
            if (obj.IsNull())
                return memberInfo.GetPropertyOrFieldType().GetDefault();

            if (memberInfo is PropertyInfo)
                return (memberInfo as PropertyInfo).GetValue(obj);

            if (memberInfo is FieldInfo)
                return (memberInfo as FieldInfo).GetValue(obj);

            throw new ArgumentException("memberType",
                $"Cannot determine type of {memberInfo.GetType().FullName} since it could not be casted to {typeof(PropertyInfo).Name} or {typeof(FieldInfo).Name}.");
        }

        public static bool IsSettable(this MemberInfo memberInfo)
        {
            if (!typeof(PropertyInfo).IsAssignableFrom(memberInfo.GetType()))
                return true;

            var setter = (memberInfo as PropertyInfo).GetSetMethod(true);

            return (setter != null);
        }

        public static object SetPropertyOrFieldValue(this MemberInfo memberInfo, object objectToUpdate, object propertyOrFieldValue)
        {
            if (memberInfo is PropertyInfo)
            {
                var propertyInfo = memberInfo as PropertyInfo;
                propertyInfo.SetValue(objectToUpdate, propertyOrFieldValue);
                return objectToUpdate;
            }

            if (memberInfo is FieldInfo)
            {
                (memberInfo as FieldInfo).SetValue(objectToUpdate, propertyOrFieldValue);
                return objectToUpdate;
            }

            throw new ArgumentException("memberType",
                $"Cannot determine type of {memberInfo.GetType().FullName} since it could not be casted to {typeof(PropertyInfo).Name} or {typeof(FieldInfo).Name}.");
        }

        public static bool TrySetPropertyOrFieldValue(this MemberInfo memberInfo, object objectToUpdate, object propertyOrFieldValue)
        {
            if (memberInfo is PropertyInfo)
            {
                var propertyInfo = memberInfo as PropertyInfo;
                if (propertyInfo.SetMethod.IsDefaultOrNull())
                    return false;

                propertyInfo.SetValue(objectToUpdate, propertyOrFieldValue);
                return true;
            }

            if (memberInfo is FieldInfo)
            {
                (memberInfo as FieldInfo).SetValue(objectToUpdate, propertyOrFieldValue);
                return true;
            }

            throw new ArgumentException("memberType",
                $"Cannot determine type of {memberInfo.GetType().FullName} since it could not be casted to {typeof(PropertyInfo).Name} or {typeof(FieldInfo).Name}.");
        }

        public static object GetPropertyOrField(this object obj, string propertyOrFieldName)
        {
            return obj
                .GetType()
                .GetProperty(propertyOrFieldName)
                .GetValue(obj)
                .ToString();
        }

        public static object Cast(this IEnumerable<object> enumerableOfObj, Type enumerableType)
        {
            var typeConvertedEnumerable = typeof(System.Linq.Enumerable)
                .GetMethod(nameof(System.Linq.Enumerable.Cast), BindingFlags.Static | BindingFlags.Public)
                .MakeGenericMethod(new Type[] { enumerableType })
                .Invoke(null, new object[] { enumerableOfObj });

            return typeConvertedEnumerable;
        }

        public static object CastArray(this IEnumerable<object> arrayOfObj, Type arrayType)
        {
            var typeConvertedEnumerable = arrayOfObj.Cast(arrayType);
            var typeConvertedArray = typeof(System.Linq.Enumerable)
                .GetMethod(nameof(System.Linq.Enumerable.ToArray), BindingFlags.Static | BindingFlags.Public)
                .MakeGenericMethod(new Type[] { arrayType })
                .Invoke(null, new object[] { typeConvertedEnumerable });
            return typeConvertedArray;
        }

        public static IEnumerable<object> ObjectToEnumerable(this object arrayOfObj)
        {
            var enumerable = (System.Collections.IEnumerable)arrayOfObj;
            foreach (var item in enumerable)
                yield return item;
        }

        public static IEnumerable<KeyValuePair<object, object>> DictionaryKeyValuePairs(this object dictionary)
        {
            if (!dictionary.GetType().IsSubClassOfGeneric(typeof(IDictionary<,>)))
                throw new ArgumentException($"{dictionary.GetType().FullName} is not of type IDictionary<>");

            foreach (var kvpObj in (dictionary as System.Collections.IEnumerable))
            {
                var kvpObjCast = KeyValuePairObject(kvpObj);
                yield return kvpObjCast;
            }
        }

        public static KeyValuePair<object, object> KeyValuePairObject(this object keyValuePair)
        {
            if (!keyValuePair.GetType().IsSubClassOfGeneric(typeof(KeyValuePair<,>)))
                throw new ArgumentException($"{keyValuePair.GetType().FullName} is not of type KeyValuePair<>");

            var kvpType = keyValuePair.GetType();
            var keyProperty = kvpType.GetProperty("Key");
            var keyValue = keyProperty.GetValue(keyValuePair);
            var valueProperty = kvpType.GetProperty("Value");
            var valueValue = valueProperty.GetValue(keyValuePair);
            return valueValue.PairWithKey(keyValue);
        }

        public static object KeyValuePairsToDictionary(this object [] kvps,
            Type keyType, Type valueType)
        {
            var dictType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
            var dict = Activator.CreateInstance(dictType);
            var addMethod = dictType.GetMethod("Add", BindingFlags.Instance | BindingFlags.Public);
            foreach (var kvpObj in kvps)
            {
                var kvpType = kvpObj.GetType();
                var keyProperty = kvpType.GetProperty("Key");
                var valueProperty = kvpType.GetProperty("Value");
                var keyValue = keyProperty.GetValue(kvpObj);
                var valueValue = valueProperty.GetValue(kvpObj);
                addMethod.Invoke(dict, new object[] { keyValue, valueValue });
            }
            return dict;
        }

        public static object ArraysToDictionary(this object[] keys, object[] values,
            Type keyType, Type valueType)
        {
            var dictType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
            var dict = Activator.CreateInstance(dictType);
            var addMethod = dictType.GetMethod("Add", BindingFlags.Instance | BindingFlags.Public);
            foreach (var kvpObj in keys.CollateSimple(values))
            {
                var keyValue = kvpObj.Item1;
                var valueValue = kvpObj.Item2;
                addMethod.Invoke(dict, 
                    new object[] { keyValue, valueValue });
            }
            return dict;
        }

        public static bool IsExtension(this MethodInfo method)
        {
            return method.IsDefaultOrNull() ?
                false
                :
                method.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false);
        }

        public static bool NullableHasValue(this object valueTypeValue)
        {
            // ?
            if (valueTypeValue is null)
                return false;

            var underlyingOrValueType = valueTypeValue.GetType();

            // If the value is not Nullable.Value == false, it is just the value.
            // Yeah, Nullable is an odd thing is C#
            if (!underlyingOrValueType.IsNullable())
                return true;

            var valueProp = underlyingOrValueType.GetProperty("HasValue",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            return (bool)valueProp.GetValue(valueTypeValue);
        }

        public static object GetNullableValue(this object valueTypeValue)
        {
            // ?
            //if (valueTypeValue == null)
            //    return null;

            var underlyingType = valueTypeValue.GetType();

            // If the value is not Nullable.Value == false, it is just the value.
            // Yeah, Nullable is an odd thing is C#
            if (!underlyingType.IsNullable())
                return valueTypeValue;

            var valueProp = underlyingType.GetProperty("Value",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            return valueProp.GetValue(valueTypeValue);
        }

        public static IEnumerable<Type> GetAllSubClassTypes(this Type type,
            Func<Assembly, bool> shouldCheckAssembly)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(shouldCheckAssembly)
                .SelectMany(
                    a =>
                    {
                        try
                        {
                            return a.GetTypes();
                        }
                        catch (Exception)
                        {
                            return new Type[] { };
                        }
                    })
                .Where(
                    (possibleMatch) =>
                    {
                        return possibleMatch
                            .IsSubClassOfGeneric(type);
                    });
        }
    }
}
