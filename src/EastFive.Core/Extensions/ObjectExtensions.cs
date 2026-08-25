using System;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using EastFive;
using EastFive.Collections.Generic;
using EastFive.Linq;
using EastFive.Reflection;
using System.Reflection;
using EastFive.Linq.Async;

namespace EastFive.Extensions
{
    public static class ObjectExtensions
    {
        public static IEnumerable<T> AsEnumerable<T>(this T firstItem,
            Func<T, IEnumerable<T>, Func<T, IEnumerable<T>>, IEnumerable<T>> nextItem
                = default(Func<T, IEnumerable<T>, Func<T, IEnumerable<T>>, IEnumerable<T>>))
        {
            yield return firstItem;
        }

        public static KeyValuePair<TKey, TValue> PairWithKey<TKey, TValue>(this TValue value, TKey key)
        {
            return new KeyValuePair<TKey, TValue>(key, value);
        }

        public static KeyValuePair<TKey, TValue> PairWithValue<TKey, TValue>(this TKey key, TValue value)
        {
            return new KeyValuePair<TKey, TValue>(key, value);
        }

        public static Task<T> AsTask<T>(this T value)
        {
            return Task.FromResult(value);
        }

        public static Task<T?> AsTaskOptional<T>(this T value)
            where T : struct
        {
            return Task.FromResult(value.AsOptional());
        }

        public static T? AsOptional<T>(this T value)
            where T : struct
        {
            return (T?)value;
        }

        public static async Task<T?> AsOptional<T>(this Task<T> value)
            where T : struct
        {
            return await value;
        }

        #region AsArray

        public static T[] AsArray<T>(this T onlyItem)
        {
            return new T[] { onlyItem };
        }

        public static T[] AsArray<T>(this T firstItem, T secondItem)
        {
            return new T[] { firstItem, secondItem, };
        }

        public static T[] AsArray<T>(this T firstItem, T secondItem, T thirdItem)
        {
            return new T[] { firstItem, secondItem, thirdItem };
        }

        public static List<T> AsList<T>(this T onlyItem)
        {
            return new List<T> { onlyItem };
        }

        #endregion

        public static IEnumerable<KeyValuePair<TKey, TValue>> PairWithValues<TKey, TValue>(this IEnumerable<TKey> keys, IEnumerable<TValue> values)
        {
            var valuesIterator = values.GetEnumerator();
            return keys.Select(
                (key, index) =>
                {
                    if (!valuesIterator.MoveNext())
                        return default(KeyValuePair<TKey, TValue>?);
                    return key.PairWithValue(valuesIterator.Current);
                })
                .SelectWhereHasValue();
        }

        public static IEnumerable<KeyValuePair<TKey, TValue>> PairWithValues<TKey, TValue>(this IEnumerable<TKey> keys, Func<TKey, TValue> calcValue)
        {
            foreach (var key in keys.NullToEmpty())
                yield return key.PairWithValue(calcValue(key));
        }

        public static RecursiveTuple<TKey> RecurseWithValue<TKey>(this TKey key, RecursiveTuple<TKey> value)
        {
            return new RecursiveTuple<TKey>()
            {
                item1 = key,
                next = () => value,
            };
        }

        public static TResult RecurseWithValue<TItem, TIndex, TResult>(this TItem key, TIndex index,
            Func<TItem, TIndex, Func<TItem, TIndex, TResult>, TResult> value)
        {
            return value(key, index,
                (itemNext, indexNext) => RecurseWithValue(itemNext, indexNext, value));
        }

        public static IDictionary<TKey, TValue> AsDictionary<TKey, TValue>(this KeyValuePair<TKey, TValue> onlyItem)
        {
            return onlyItem.AsEnumerable().ToDictionary();
        }

        public static T OrIfDefault<T>(this T value, T alternative)
            where T : IComparable
        {
            if (value.IsDefault())
                return alternative;
            return value;
        }

        public static T OrIfDefaultOrNull<T>(this T value, T alternative)
            where T : class
        {
            if (value.IsDefaultOrNull())
                return alternative;
            return value;
        }

        public static bool IsDefault<T>(this T value)
        {
            return EqualityComparer<T>.Default.Equals(value, default(T));
        }

        public static bool IsNotDefaultOrNull<T>(this T value)
           where T : class
        {
            return !value.IsDefaultOrNull();
        }

        public static bool IsDefaultOrNull<T>([NotNull]this T value)
           where T : class
        {
            return EqualityComparer<T>.Default.Equals(value, default(T));
        }

#nullable enable

        public static bool TryIsNotDefaultOrNull<T>(this T? valueMaybe, [NotNullWhen(true)] out T? value)
           where T : class
        {
            if(EqualityComparer<T>.Default.Equals(valueMaybe, default))
            {
                value = default;
                return false;
            }
            value = valueMaybe;
            return true;
        }

#nullable restore

        public static bool IsNull<T>(this T value)
           where T : class
        {
            return value == null;
        }

        public static bool IsDefaultNullOrEmpty<T>(this IEnumerable<T> value)
        {
            if (value.IsDefaultOrNull())
                return true;
            return (!value.Any());
        }

        public static bool HasSetValue<T>(this T value)
           where T : class
        {
            return !value.IsDefaultOrNull();
        }

        public static TResult HasValue<T, TResult>(this T value, Func<T, TResult> hasValue, Func<TResult> nullOrEmptyValue)
           where T : class
        {
            if (value.IsDefaultOrNull())
                return nullOrEmptyValue();
            return hasValue(value);
        }

        public static bool IsDefaultOrEmpty(this Guid value)
        {
            return value.IsDefault() || value == Guid.Empty;
        }

        public static TResult HasValue<T, TResult>(this Nullable<T> value, Func<T, TResult> hasValue, Func<TResult> nullOrEmptyValue)
            where T : struct
        {
            if (!value.HasValue)
                return nullOrEmptyValue();
            return hasValue(value.Value);
        }

        public static bool Equals<T>(this Nullable<T> value1, Nullable<T> value2)
           where T : struct
        {
            if (value1.HasValue)
            {
                if (value2.HasValue)
                    return ValueType.Equals(value1.Value, value2.Value);
                return false;
            }
            return !value2.HasValue;
        }

        public static TResult HasValue<TResult>(this Guid value, Func<Guid, TResult> hasValue, Func<TResult> nullOrEmptyValue)
        {
            if (value.IsDefaultOrEmpty())
                return nullOrEmptyValue();
            return hasValue(value);
        }

        public static TResult HasValue<TResult>(this DateTime value, Func<DateTime, TResult> hasValue, Func<TResult> nullOrEmptyValue)
        {
            if (value.IsDefault())
                return nullOrEmptyValue();
            return hasValue(value);
        }

        public static T? ToOptional<T>(this T value)
            where T : struct
        {
            return (T?)value;
        }

        public static bool IsDefault(this Uri value)
        {
            return value.IsDefaultOrNull();
        }

        public static T IfThen<T>(this T value, bool ifCondition,
            Func<T, T> thenOperation)
        {
            if (ifCondition)
                return thenOperation(value);
            return value;
        }

        public static TResult IfThenElse<T, TResult>(this T value, bool ifCondition,
            Func<T, TResult> thenOperation,
            Func<T, TResult> elseOperation)
        {
            if (ifCondition)
                return thenOperation(value);
            return elseOperation(value);
        }

        public static bool TryGetType(this object value, out Type type)
        {
            if(value.IsNull())
            {
                type = default;
                return false;
            }
            type = value.GetType();
            return true;
        }

        public static TResult TryAsDecimal<TResult>(this object value,
            Func<decimal, TResult> onCasted,
            Func<TResult> onCouldNotCast)
        {
            if (value is decimal)
            {
                var decimalValue = (decimal)value;
                return onCasted(decimalValue);
            }
            if (value is int)
            {
                var intValue = (int)value;
                return onCasted(intValue);
            }
            if (value is long)
            {
                var longValue = (long)value;
                return onCasted(longValue);
            }
            if (value is float)
            {
                var floatValue = (float)value;
                return onCasted((decimal)floatValue);
            }
            if (value is double)
            {
                var doubleValue = (double)value;
                return onCasted((decimal)doubleValue);
            }
            return onCouldNotCast();
        }

        /// <summary>
        /// Clones a object via shallow copy
        /// </summary>
        /// <typeparam name="T">Object Type to Clone</typeparam>
        /// <param name="obj">Object to Clone</param>
        /// <returns>New Object reference</returns>
        public static T CloneObject<T>(this T obj) where T : class
        {
            if (obj is null)
                return null;

            if (typeof(ICloneable).IsAssignableFrom(typeof(T)))
            {
                var cloneable = (ICloneable)obj;
                return (T)cloneable.Clone();
            }

            var memberwiseCloneMethod = obj.GetType().GetMethod("MemberwiseClone",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (memberwiseCloneMethod != null)
                return (T)memberwiseCloneMethod.Invoke(obj, null);
            
            return null;
        }
        
        public static bool IsEqualTObjectPropertiesWithAttributeInterface<T, TAttr>(this T obj, T objectToCompare,
            Func<MemberInfo, TAttr, object, object, bool> areEqual,
            bool skipNullAndDefault = false, bool inherit = false)
        {
            if (obj is null)
                return objectToCompare is null;

            if (objectToCompare is null)
                return false;
            var attributeInterface = typeof(TAttr);
            return typeof(T)
                .GetPropertyAndFieldsWithAttributesInterface<TAttr>(inherit: inherit)
                .All(
                    (memberAndAttr) =>
                    {
                        var value1 = memberAndAttr.Item1.GetPropertyOrFieldValue(obj);
                        var value2 = memberAndAttr.Item1.GetPropertyOrFieldValue(objectToCompare);
                        if (skipNullAndDefault)
                            if (value1.IsDefaultOrNull() || value2.IsDefaultOrNull())
                                return true;

                        return areEqual(memberAndAttr.Item1, memberAndAttr.Item2,
                                value1, value2);
                    });
        }

        public static T CloneObjectPropertiesWithAttributeInterface<T>(this T obj, T objectToUpdate,
            Type attributeInterface, bool skipNullAndDefault = false, bool inherit = false)
        {
            if (obj is null)
                return objectToUpdate;

            if (objectToUpdate is null)
                return objectToUpdate;

            return typeof(T)
                .GetPropertyAndFieldsWithAttributesInterface(attributeInterface, inherit: inherit)
                .Aggregate(
                    objectToUpdate,
                    (objToUpdate, memberInfo) =>
                    {
                        var v = memberInfo.GetPropertyOrFieldValue(obj);
                        if (skipNullAndDefault)
                        {
                            if (v.IsDefaultOrNull())
                                return objectToUpdate;
                        }
                        objToUpdate = (T)memberInfo.SetPropertyOrFieldValue(objToUpdate, v);
                        return objToUpdate;
                    });
        }
    }
}
