using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{

    public interface IDocumentRoute
    {
        Resources.Route GetRoute(Type type, HttpApplication httpApp);
    }
}
