using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EastFive.Extensions;

namespace EastFive.Analytics
{
    public static class LoggingExtensions
    {
        public static ILogger CreateScope(this ILogger logger, string state)
        {
            if (!logger.IsDefault())
                return new ScopedLogger(state, logger);
            return logger;
        }

        public static void Information(this ILogger logger, string message)
        {
            if (logger.IsDefault())
                return;
            logger.LogInformation(message);
        }

        public static void Trace(this ILogger logger, string message)
        {
            if (logger.IsDefault())
                return;
            logger.LogTrace(message);
        }

        public static void Warning(this ILogger logger, string message)
        {
            if (logger.IsDefault())
                return;
            logger.LogWarning(message);
        }

        public static void Critical(this ILogger logger, string message)
        {
            if (logger.IsDefault())
                return;
            logger.LogCritical(message);
        }
    }
}
