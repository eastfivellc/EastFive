using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class ExtensionAttribute : Attribute
    {
        public Type ExtendedResourceType { get; set; }

        public ExtensionAttribute(Type extendedResourceType)
        {
            this.ExtendedResourceType = extendedResourceType;
        }
    }
}
