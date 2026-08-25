using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public interface IHaveMoreWork
    {
        Task ProcessWorkAsync(CancellationToken cancellationToken);
    }
}
