using EastFive.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Reflection
{
    public static class TaskExtensions
    {
        public static Task<object> CastAsTaskObjectAsync(this object taskObj, out Type resultType)
        {
            var taskType = taskObj.GetType();
            if (!taskType.IsSubClassOfGeneric(typeof(Task<>)))
                throw new ArgumentException($"{taskType.FullName} is not of type Task<>");
            resultType = taskType.GenericTypeArguments.First();
            var castTaskMethodGeneric = typeof(TaskExtensions).GetMethod(nameof(CastTaskAsObjectInner), BindingFlags.Static | BindingFlags.Public);
            var castTaskMethod = castTaskMethodGeneric.MakeGenericMethod(new Type[] { resultType });
            var objCastTask = castTaskMethod.Invoke(null, new object[] { taskObj });
            var taskForObject = (objCastTask as Task<object>);
            return taskForObject;
        }

        public static object ConstructEmptyArray(this Type arrayElementType) => Array.CreateInstance(arrayElementType, 0);

        public static IEnumerable<object> ConstructEnumerableOfType(this Type arrayElementType, int length)
        {
            var enumerator = Array
                .CreateInstance(arrayElementType, length)
                .GetEnumerator();
            while (enumerator.MoveNext())
                yield return enumerator.Current;
        }

        public static Task<T> CastTask<T>(this object taskObj)
        {
            var taskType = taskObj.GetType();
            if (!taskType.IsSubClassOfGeneric(typeof(Task<>)))
                throw new ArgumentException($"{taskType.FullName} is not of type Task<>");
            var resultType = taskType.GenericTypeArguments.First();
            var castTaskMethodGeneric = typeof(TaskExtensions).GetMethod(nameof(CastTaskInner), BindingFlags.Static | BindingFlags.Public);
            var castTaskMethod = castTaskMethodGeneric.MakeGenericMethod(new Type[] { resultType, typeof(T) });
            var objCastTask = castTaskMethod.Invoke(null, new object[] { taskObj });
            return objCastTask as Task<T>;
        }

        /// <summary>
        /// Returns a Task&lt;T&gt; where T is equal to <paramref name="valueToWrap">valueToWrap</paramref>.GetType().
        /// </summary>
        /// <param name="valueToWrap"></param>
        /// <returns></returns>
        public static object ValueAsTaskOfType(this object valueToWrap)
        {
            var valueType = valueToWrap.GetType();
            var fromResultMethod = typeof(Task)
                .GetMethod(nameof(Task.FromResult))
                .MakeGenericMethod(valueType.AsArray());
            var taskForValue = fromResultMethod.Invoke(null, valueToWrap.AsArray());
            return taskForValue;
        }

        public static async Task<TResult> CastTaskInner<T, TResult>(Task<T> task)
        {
            var t = await task;
            var tObj = (object)t;
            return (TResult)tObj;
        }

        public static async Task<object> CastTaskAsObjectInner<T>(Task<T> task)
        {
            var t = await task;
            var tObj = (object)t;
            return tObj;
        }

    }
}
