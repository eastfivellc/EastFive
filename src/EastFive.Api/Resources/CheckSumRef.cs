using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public struct CheckSumRef<TResource> 
        where TResource : IReferenceable
    {
        public CheckSumRef(Guid id, string checkSumValue)
        {
            this.resourceRef = id.AsRef<TResource>();
            this.checkSumValue = checkSumValue;
        }

        public IRef<TResource> resourceRef;
        public string checkSumValue;
    }
}
