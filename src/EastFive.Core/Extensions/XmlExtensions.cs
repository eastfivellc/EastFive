using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace EastFive
{
    public static class XmlExtensions
    {
        public static IEnumerable<XmlNode> EnumerateNodes(this XmlNodeList nodeList)
        {
            foreach (XmlNode node in nodeList)
                yield return node;
        }
    }
}
