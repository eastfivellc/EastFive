using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.XPath;

namespace EastFive.Xml
{
    interface IExtendXPath
    {
        string Name { get; }

        object BindArgumentToParameter(object arg, ParameterInfo parameter);

        XPathResultType BindReturnType(MethodInfo returnType);
    }
}
