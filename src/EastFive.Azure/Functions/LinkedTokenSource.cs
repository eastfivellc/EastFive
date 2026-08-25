using System;
using System.Threading;
using System.Threading.Tasks;

namespace EastFive.Azure.Functions
{
    // Example of how to use this class:
    //
    //[FunctionName("cancelTokenTest")]
    //public static async Task CancelTest(
    //    [TimerTrigger(scheduleExpression: "0 */10 * * * *", RunOnStartup = false)]TimerInfo myTimer,
    //    System.Threading.CancellationToken functionToken,
    //    Microsoft.Extensions.Logging.ILogger log)
    //{
    //    var logger = new AnalyticsLogger(log);
    //    var linkedToken = new LinkedTokenSource(functionToken,
    //        async (cancelledByAzure, cancelledManually) =>
    //        {
    //            logger.Trace($"Function terminated by Azure: {cancelledByAzure}");
    //            logger.Trace($"Function terminated by us: {cancelledManually}");
    //            await Task.Delay(TimeSpan.FromSeconds(5)); // do any cleanup work
    //            logger.Trace("Function ended");
    //        });

    //    // uncomment to test manually cancelling this token
    //    linkedToken.Source.Cancel();

    //    // can avoid TaskCancelledException or further processing if we check this flag
    //    if (linkedToken.IsCancellationRequested)
    //        return;

    //    // Azure will cancel us before 20 minutes
    //    await Task.Delay(TimeSpan.FromMinutes(20), linkedToken.Token);
    //}

    public class LinkedTokenSource : IDisposable
    {
        private CancellationTokenSource ourSource;
        private CancellationTokenSource linkedSource;
        private CancellationTokenRegistration registration;

        public LinkedTokenSource(CancellationToken theirToken,
            Func<bool, // their token was cancelled
                bool, // our token was cancelled
                Task> whenCancelledAsync,
            CancellationTokenSource ourSource = default)
        {
            this.ourSource = ourSource != default ? ourSource : new CancellationTokenSource();
            this.linkedSource = CancellationTokenSource.CreateLinkedTokenSource(this.ourSource.Token, theirToken);
            this.registration = this.linkedSource.Token.Register(
                async () =>
                {
                    var theirsCancelled = theirToken.IsCancellationRequested;
                    var oursCancelled = this.ourSource.Token.IsCancellationRequested;
                    await whenCancelledAsync(theirsCancelled, oursCancelled);
                },
                true);
        }

        public CancellationToken Token => linkedSource.Token;

        public bool IsCancellationRequested => linkedSource.Token.IsCancellationRequested;

        public CancellationTokenSource Source => ourSource;

        public void Dispose()
        {
            if (ourSource != default)
            {
                ourSource.Dispose();
                ourSource = default;
            }
            if (linkedSource != default)
            {
                linkedSource.Dispose();
                linkedSource = default;
            }
            if (registration != default)
            {
                registration.Dispose();
                registration = default;
            }
        }
    }
}