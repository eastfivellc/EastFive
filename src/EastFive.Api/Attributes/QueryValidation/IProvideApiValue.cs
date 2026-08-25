using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace EastFive.Api
{
    public interface IProvideApiValue
    {
        // string PropertyName { get; }

        string GetPropertyName(MemberInfo member);

    }

    
}
