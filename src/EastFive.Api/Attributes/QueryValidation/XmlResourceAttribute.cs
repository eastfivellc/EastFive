using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using EastFive.Extensions;
using System.Xml;
using EastFive.Linq;
using System.Xml.Serialization;
using EastFive.Serialization;
using EastFive.Api.Serialization;
using System.IO;
using EastFive.Collections.Generic;

namespace EastFive.Api
{
    public class XmlResourceAttribute : PropertyAttribute, IBindApiValue, IBindXmlApiValue
    {
        public TResult ParseContentDelegate<TResult>(XmlDocument xmlDoc, string rawContent,
                ParameterInfo parameterInfo, IApplication httpApp, IHttpRequest request,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            if (parameterInfo.ParameterType.IsAssignableFrom(typeof(XmlDocument)))
                return onParsed(xmlDoc);

            if (parameterInfo.ParameterType.IsAssignableFrom(typeof(string)))
                return onParsed(rawContent);

            return onFailure($"Cannot bind XML Resource to `{parameterInfo.ParameterType.FullName}.`");
        }
    }
}
