using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public interface IIdentifyResource
    {
    }

    public class ResourceIdentifierAttribute : Attribute, IIdentifyResource
    {
    }

    public interface ITitleResource
    {
    }

    public class ResourceTitleAttribute : Attribute, ITitleResource
    {
    }
}
