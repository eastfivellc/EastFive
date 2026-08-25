using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Analytics
{
    class ScopedLogger : ILogger
    {
        private string state;
        private ILogger logger;

        public ScopedLogger(string state, ILogger logger)
        {
            this.state = state;
            this.logger = logger;
        }

        public void LogInformation(string message)
        {
            logger.LogInformation($"{state}:{message}");
        }

        public void LogTrace(string message)
        {
            logger.LogTrace($"{state}:{message}");
        }

        public void LogWarning(string message)
        {
            logger.LogWarning($"{state}:{message}");
        }

        public void LogCritical(string message)
        {
            logger.LogCritical($"{state}:{message}");
        }
    }
}
