using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Reflection
{
    public struct Assignment
    {
        public MemberInfo member;
        public ExpressionType type;
        public object value;
    }
}
