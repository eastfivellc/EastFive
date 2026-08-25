using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class ResourceCantBeUpdatedException : ResourceConflictException, IHttpResponseMessageException
    {
        public ResourceCantBeUpdatedException()
        {
        }

        public ResourceCantBeUpdatedException(string message) : base(message)
        {
        }
    }
}
