using EastFive.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Analytics
{
    public class CaptureLog : ILogger
    {
        private ILogger passthrough;
        private Queue<string> logMessages;
        private Stopwatch timer;

        public CaptureLog(ILogger passthrough, Stopwatch timer = default)
        {
            this.passthrough = passthrough;
            this.logMessages = new Queue<string>();
            this.timer = timer;
        }

        public void LogInformation(string message)
        {
            this.logMessages.Enqueue($"INFORMATION:{message}");
            passthrough.Information(message);
        }

        public void LogTrace(string message)
        {
            if (this.timer.IsDefaultOrNull())
            {
                this.logMessages.Enqueue($"TRACE:{message}");
                passthrough.Trace(message);
                return;
            }
            var elapsed = timer.Elapsed;
            this.logMessages.Enqueue(String.Format("TRACE[{0:00.000}]:{1}", elapsed.TotalSeconds, message));
            passthrough.Trace(message);
        }

        public void LogWarning(string message)
        {
            this.logMessages.Enqueue($"WARNING:{message}");
            passthrough.Warning(message);
        }

        public void LogCritical(string message)
        {
            this.logMessages.Enqueue($"WARNING:{message}");
            passthrough.Critical(message);
        }

        public string[] Dump()
        {
            return this.logMessages.ToArray();
        }
    }
}
