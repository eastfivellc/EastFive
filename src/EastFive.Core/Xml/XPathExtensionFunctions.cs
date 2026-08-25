using EastFive.Extensions;
using EastFive.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.XPath;

namespace EastFive.Xml
{
    public class XPathExtensionFunctions : System.Xml.Xsl.IXsltContextFunction
    {
        // The name of the extension function.
        protected string FunctionName { get; private set; }

        // Constructor used in the ResolveFunction method of the custom XsltContext 
        // class to return an instance of IXsltContextFunction at run time.
        public XPathExtensionFunctions(int minArgs, int maxArgs,
            XPathResultType returnType, XPathResultType[] argTypes, string functionName)
        {
            this.Minargs = minArgs;
            this.Maxargs = maxArgs;
            this.ReturnType = returnType;
            this.ArgTypes = argTypes;
            this.FunctionName = functionName;
        }

        #region IXsltContextFunction

        #region IXsltContextFunction argument properties

        // The data types of the arguments passed to XPath extension function.
        public XPathResultType[] ArgTypes { get; private set; }

        // The maximum number of arguments that can be passed to function.
        public int Maxargs { get; private set; }

        // The minimum number of arguments that can be passed to function.
        public int Minargs { get; private set; }

        // The data type returned by extension function.
        public XPathResultType ReturnType { get; private set; }

        #endregion

        // Function to execute a specified user-defined XPath extension 
        // function at run time.
        public object Invoke(System.Xml.Xsl.XsltContext xsltContext,
                       object[] args, System.Xml.XPath.XPathNavigator docContext)
        {
            var result = GetMatchingFunction(this.FunctionName, this.GetType(),
                (method, xpathExtensionAttr) =>
                {
                    var methodArgs = args
                        .Zip(method.GetParameters(),
                            (arg, parameter) => xpathExtensionAttr.BindArgumentToParameter(arg, parameter))
                        .ToArray();
                    return method.Invoke(this, methodArgs);
                },
                () =>
                {
                    var msgLine1 = $"`{this.GetType().FullName}` does not contain a method" +
                            $" that implements `{this.FunctionName}` with an Attribute that extends {typeof(IExtendXPath).FullName}";
                    var msgLine2 = $"\nEnsure there is a single attribute extending {typeof(IExtendXPath).FullName} a method" +
                        $" named, or with {typeof(IExtendXPath).FullName}.Name equal to `{this.FunctionName}` on {this.GetType().FullName}.";
                    throw new Exception(msgLine1 + msgLine2);
                });
            return result;
        }

        #endregion

        internal static TResult GetMatchingFunction<TXPathExtensionFunctions, TResult>(string functionName,
            Func<MethodInfo, IExtendXPath, TResult> onMatch,
            Func<TResult> onNoMatches)
        {
            return GetMatchingFunction(functionName, typeof(TXPathExtensionFunctions),
                onMatch,
                onNoMatches);
        }

        internal static TResult GetMatchingFunction<TResult>(string functionName, Type xPathExtensionFunctionsType,
            Func<MethodInfo, IExtendXPath, TResult> onMatch,
            Func<TResult> onNoMatches)
        {
            return xPathExtensionFunctionsType
                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Where(method => method.ContainsAttributeInterface<IExtendXPath>(true))
                .Select(method => method.GetAttributesInterface<IExtendXPath>().PairWithValue(method))
                .Where(xpathExtensionAttrsMethodKvp => xpathExtensionAttrsMethodKvp.Key.One())
                .Where(
                    xpathExtensionAttrsMethodKvp =>
                    {
                        var xpathExtensionAttr = xpathExtensionAttrsMethodKvp.Key.Single();
                        var method = xpathExtensionAttrsMethodKvp.Value;
                        var xPathMethodName = xpathExtensionAttr.Name.HasBlackSpace() ?
                            xpathExtensionAttr.Name
                            :
                            method.Name;
                        return xPathMethodName == functionName;
                    })
                .First(
                    (xpathExtensionAttrsMethodKvp, next) =>
                        onMatch(xpathExtensionAttrsMethodKvp.Value, 
                            xpathExtensionAttrsMethodKvp.Key.Single()),
                    onNoMatches);
        }

    }
}
