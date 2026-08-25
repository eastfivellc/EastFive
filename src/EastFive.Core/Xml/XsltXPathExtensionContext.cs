using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.XPath;
using System.Xml.Xsl;

namespace EastFive.Xml
{
    public delegate TXPathExtensionFunctions ConstructContextFunctionDelegate<TXPathExtensionFunctions>(
            int minArgs, int maxArgs,
            XPathResultType returnType, XPathResultType[] argTypes, string functionName)
        where TXPathExtensionFunctions : XPathExtensionFunctions;

    public class XsltXPathExtensionContext<TXPathExtensionFunctions> : XsltContext
        where TXPathExtensionFunctions : XPathExtensionFunctions
    {
        protected ConstructContextFunctionDelegate<TXPathExtensionFunctions> constructContext;

        public XsltXPathExtensionContext(ConstructContextFunctionDelegate<TXPathExtensionFunctions> constructContext)
        {
            this.constructContext = constructContext;
        }

        public override IXsltContextFunction ResolveFunction(string prefix, string name, XPathResultType[] argTypes)
        {
            return XPathExtensionFunctions.GetMatchingFunction<TXPathExtensionFunctions, IXsltContextFunction>(
                name,
                (method, attr) =>
                {
                    var argsMin = method.GetParameters().Where(param => !param.IsOptional).Count();
                    var argsMax = method.GetParameters().Count();
                    var returnType = attr.BindReturnType(method);
                    return constructContext(argsMin, argsMax, returnType, argTypes, name);
                },
                () => default(IXsltContextFunction));
        }

        public override IXsltContextVariable ResolveVariable(string prefix, string name)
        {
            throw new NotImplementedException();
        }

        #region XsltContext options

        public override bool Whitespace => true;

        public override int CompareDocument(string baseUri, string nextbaseUri)
        {
            return baseUri.CompareTo(nextbaseUri);
        }

        public override bool PreserveWhitespace(XPathNavigator node)
        {
            return false;
        }

        #endregion
    }
}
