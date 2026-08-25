using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using EastFive.Api;
using EastFive;
using EastFive.Linq;
using EastFive.Extensions;

namespace EastFive.Api
{
    public static class TypeExtensions
    {
        public static TResult GetRouteName<TResult>(this Type type,
            Func<string, TResult> onFoundRouteName,
            Func<TResult> onNotAController = default)
        {
            return type.GetAttributesInterface<IInvokeResource>()
                .First(
                    (attr, next) => onFoundRouteName(attr.Route),
                    () => onNotAController());
        }

        //public static TResult GetFileParameters<TResult>(this Type type,
        //    Func<string[], TResult> onFoundRouteName,
        //    Func<TResult> onNotAController = default)
        //{
        //    return type.GetAttributesInterface<IInvokeResource>()
        //        .First(
        //            (attr, next) => onFoundRouteName(attr.ParseFilenameParameters()),
        //            () => onNotAController());
        //}

        public static string AsJsonType(this Type type)
        {
            if (typeof(int) == type)
                return "integer";
            if (typeof(long) == type)
                return "integer";
            if (typeof(double) == type)
                return "number";
            if (typeof(float) == type)
                return "number";
            if (typeof(decimal) == type)
                return "number";
            if (typeof(bool) == type)
                return "boolean";
            if (typeof(string) == type)
                return "string";

            if (type.IsArray)
                return "array";

            return "object";
        }
    }
}
