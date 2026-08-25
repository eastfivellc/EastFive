using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using EastFive.Collections.Generic;

namespace EastFive.Reflection
{
    public static class ObjectBuilder
    {
        public static T PopulateType<T>(this T obj, IDictionary<PropertyInfo, object> properties)
        {
            // .GetType() must be used here instead of typeof(T) because
            // the calling method may invoke with a parent class
            var objectProperties = properties.SelectKeys();

            foreach (var objectProperty in objectProperties)
            {
                var value = properties[objectProperty];
                objectProperty.SetValue(obj, value);
            }

            return obj;
        }

        public static TSubclass PopulateSubclassWithParentValues<TBase, TSubclass>(this TBase baseObj, TSubclass subclassObj)
        {
            return typeof(TBase)
                .GetPropertyOrFieldMembers()
                .Where(
                    mi => mi.IsSettable())
                .Aggregate(
                    subclassObj,
                    (objToPop, member) =>
                    {

                        var value = member.GetPropertyOrFieldValue(baseObj);
                        return (TSubclass)member.SetPropertyOrFieldValue(objToPop, value);
                    });
        }
    }
}