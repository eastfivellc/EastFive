using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class ResourceAlreadyExistsException : ResourceConflictException, IHttpResponseMessageException
    {
        public ResourceAlreadyExistsException()
            : base("The resource already exists.")
        {
        }
    }
}
