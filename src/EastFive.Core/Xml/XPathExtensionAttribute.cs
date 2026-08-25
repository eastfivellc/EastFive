using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.XPath;

using EastFive.Extensions;
using EastFive.Reflection;

namespace EastFive.Xml
{
    public class XPathExtensionAttribute : Attribute, IExtendXPath
    {
        public string Name { get; set; }

        public virtual object BindArgumentToParameter(object arg, ParameterInfo parameter)
        {
            return BindArgumentToType(arg, parameter.ParameterType);
        }

        public virtual object BindArgumentToType(object arg, Type type)
        {
            var argType = arg.GetType();
            if (type == typeof(string))
            {
                if (arg is XPathNodeIterator)
                {
                    var iterator = arg as XPathNodeIterator;
                    if (!iterator.MoveNext())
                        return null;
                    return iterator.Current.Value;
                }
                if (arg is XPathNavigator)
                {
                    return (arg as XPathNavigator).Value;
                }
                return Convert.ToString(arg);
            }

            if (type.IsAssignableFrom(typeof(int)))
                return Convert.ToInt32(arg);

            if (type.IsAssignableFrom(typeof(short)))
                return Convert.ToInt16(arg);

            if (type.IsAssignableFrom(argType))
                return arg;

            if (type.IsArray)
            {
                if (arg is XPathNodeIterator)
                {
                    IEnumerable<XPathNavigator> Iterate(XPathNodeIterator iterator)
                    {
                        //if (!iterator.Current.IsDefaultOrNull())
                        //    yield return iterator.Current;

                        while (iterator.MoveNext())
                            yield return iterator.Current;
                    }
                    var arrayType = type.GetElementType();
                    return Iterate(arg as XPathNodeIterator)
                        .Select(item => BindArgumentToType(item, arrayType))
                        .CastArray(arrayType);
                }
            }

            throw new Exception($"Cannot bind {argType.FullName} to {type.FullName} in {typeof(XPathExtensionAttribute).FullName}.");
        }

        public XPathResultType BindReturnType(MethodInfo method)
        {
            var returnType = method.ReturnType;
            if (returnType == typeof(object))
                return XPathResultType.Any;
            if (typeof(XPathNavigator).IsAssignableFrom(returnType))
                return XPathResultType.Navigator;
            if (returnType.IsAssignableFrom(typeof(string)))
                return XPathResultType.String;
            if (returnType.IsAssignableFrom(typeof(bool)))
                return XPathResultType.Boolean;
            if(returnType.IsNumeric())
                return XPathResultType.Number;
            if (returnType.IsArray)
                return XPathResultType.NodeSet;
            if (returnType == typeof(XPathNodeIterator))
                return XPathResultType.NodeSet;
            return XPathResultType.Any;
        }
    }
}
