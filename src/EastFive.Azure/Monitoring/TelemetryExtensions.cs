using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;

namespace EastFive.Azure.Monitoring
{
    public static class TelemetryExtensions
    {
        private static readonly TelemetryClient client;

        static TelemetryExtensions()
        {
            
            client = ApplicationInsightsTelemetryConfiguration.Current()?.ConnectionString is { } connectionString
                    && !string.IsNullOrWhiteSpace(connectionString)
                ? new TelemetryClient(new TelemetryConfiguration() { ConnectionString = connectionString })
#pragma warning disable 0618 // TelemetryConfiguration.Active: the SDK's own fallback when no connection string is configured.
                : new TelemetryClient(TelemetryConfiguration.Active);
#pragma warning restore 0618
        }

        public static TelemetryClient LoadTelemetryClient()
        {
            // sharing this instance so that we don't leak handles/threads from repeated instantiations
            return client;
        }
    }
}
