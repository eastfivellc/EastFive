using EastFive.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EastFive.Linq;

namespace EastFive
{
    public static class NullableExtensions
    {
        public static TResult GetValueOrDefault<T, TResult>(this Nullable<T> valueMaybe,
            Func<T, TResult> onValue,
            TResult defaultValue)
            where T : struct
        {
            if (!valueMaybe.HasValue)
                return defaultValue;
            var result = onValue(valueMaybe.Value);
            return result;
        }

        public static object AsNullable(this object valueTypeValue)
        {
            var underlyingType = valueTypeValue.GetType();
            var nullableType = typeof(Nullable<>).MakeGenericType(underlyingType);
            if (!underlyingType.IsValueType)
                throw new ArgumentException("structValue", $"Type `{underlyingType.FullName}` passed to AsNullable is not a value type.");
            // new Nullable<int>(10);
            var nullableInstance = Activator.CreateInstance(nullableType, new object[] { valueTypeValue });
            return nullableInstance;
        }

    }
}
