using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Collections.Generic
{
    public static class ComparisonExtensions
    {
        private class Comparer<T> : System.Collections.Generic.Comparer<T>
        {
            public Func<T, T, int> compare;

            public override int Compare(T x, T y)
            {
                return compare(x, y);
            }
        }

        private class EqualityComparerPredicate<T> : System.Collections.Generic.EqualityComparer<T>
        {
            public Func<T, T, bool> areEqual;
            public Func<T, int> hash;

            public override bool Equals(T x, T y)
            {
                return areEqual(x, y);
            }

            public override int GetHashCode(T obj)
            {
                return hash(obj);
            }
        }

        public static IComparer<T> ToComparer<T>(this Func<T, T, int> compare)
        {
            var comparer = new Comparer<T>();
            comparer.compare = compare;
            return comparer;
        }

        public static IEqualityComparer<T> ToEqualityComparer<T>(this Func<T, T, int> compare,
            Func<T, int> hash = default(Func<T, int>))
        {
            Func<T, T, bool> areEqual = (v1, v2) => compare(v1, v2) == 0;
            return areEqual.ToEqualityComparer(hash);
        }

        public static IEqualityComparer<T> ToEqualityComparer<T>(this Func<T, T, bool> areEqual,
            Func<T, int> hash = default(Func<T, int>))
        {
            var comparer = new EqualityComparerPredicate<T>();
            comparer.areEqual = areEqual;
            comparer.hash = (default(Func<T, int>) == hash) ?
                (obj) => obj.GetHashCode()
                :
                hash;

            return comparer;
        }
        
        public static bool IsEqualToKeyValuePairArray<TValue>(
            this KeyValuePair<string, TValue>[] array1, 
            KeyValuePair<string, TValue>[] array2,
            Func<TValue, TValue, bool> valueComparer)
        {
            // Check if both arrays are null
            if (array1 == null && array2 == null)
                return true;
            
            // Check if only one is null
            if (array1 == null || array2 == null)
                return false;
            
            // Check if arrays have different lengths
            if (array1.Length != array2.Length)
                return false;
            
            // Convert arrays to dictionaries for efficient lookup
            var dict1 = array1.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            var dict2 = array2.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            
            // Check if all keys in dict1 exist in dict2 with equal values
            foreach (var kvp in dict1)
            {
                if (!dict2.TryGetValue(kvp.Key, out TValue value2))
                    return false;
                
                if (!valueComparer(kvp.Value, value2))
                    return false;
            }
            
            // Check if dict2 has any keys not in dict1
            if (dict2.Keys.Any(key => !dict1.ContainsKey(key)))
                return false;
            
            return true;
        }
    }
}
