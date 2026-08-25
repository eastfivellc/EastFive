using System;
using System.ComponentModel;
using System.Reflection;

namespace EastFive.Extensions
{
    public static class EnumExtensions
    {
        /// <summary>
        /// When an enum value has a Description attribute, it will be returned rather than the constant name.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string GetDescriptionOrName(this Enum value)
        {
            Type type = value.GetType();
            string name = Enum.GetName(type, value);
            if (null != name)
            {
                FieldInfo field = type.GetField(name);
                if (null != field)
                {
                    if (Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) is DescriptionAttribute attr)
                        return attr.Description;
                }
            }
            return name ?? string.Empty;
        }

        public static TResult AsEnum<T, TResult>(this int enumIntValue,
            Func<T, TResult> onValidValue,
            Func<TResult> onInvalidValue)
            where T : Enum
        {
            if (!Enum.IsDefined(typeof(T), enumIntValue))
                return onInvalidValue();

            var enumValue = (T)Enum.ToObject(typeof(T), enumIntValue);
            return onValidValue(enumValue);
        }
    }
}
