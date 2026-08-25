using System;
using System.Diagnostics;

namespace EastFive.Analytics
{
    public interface ILogger
    {
        void LogInformation(string message);

        void LogTrace(string message);

        void LogWarning(string message);

        void LogCritical(string message);
    }

    public delegate void OnMessageHandler(string message);

    public interface ILoggerWithEvents : ILogger
    {
        event OnMessageHandler OnInformation;

        event OnMessageHandler OnTrace;

        event OnMessageHandler OnWarning;
    }

    public class DebugLogger : ILogger
    {
        public void LogInformation(string message)
        {
            Debug.WriteLine(message);
        }

        public void LogTrace(string message)
        {
            Debug.WriteLine(message);
        }

        public void LogWarning(string message)
        {
            Debug.WriteLine(message);
        }

        public void LogCritical(string message)
        {
            Debug.WriteLine(message);
        }
    }

    public class ConsoleLogger : ILogger
    {
        public void LogInformation(string message)
        {
            Console.WriteLine(message);
        }

        public void LogTrace(string message)
        {
            Console.WriteLine(message);
        }

        public void LogWarning(string message)
        {
            Console.WriteLine(message);
        }

        public void LogCritical(string message)
        {
            Console.WriteLine(message);
        }
    }

    public class EventLogger : ILoggerWithEvents
    {
        private ILogger logger;

        public EventLogger(ILogger logger)
        {
            this.logger = logger;
        }

        public event OnMessageHandler OnInformation;
        public event OnMessageHandler OnTrace;
        public event OnMessageHandler OnWarning;
        public event OnMessageHandler OnCritical;

        public void LogInformation(string message)
        {
            OnInformation(message);
            logger.Information(message);
        }

        public void LogTrace(string message)
        {
            OnTrace(message);
            logger.Trace(message);
        }

        public void LogWarning(string message)
        {
            OnWarning(message);
            logger.Warning(message);
        }

        public void LogCritical(string message)
        {
            OnCritical(message);
            logger.LogCritical(message);
        }
    }
}