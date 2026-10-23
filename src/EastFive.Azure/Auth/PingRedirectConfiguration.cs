using System;

namespace EastFive.Azure.Auth
{
    /// <summary>
    /// Per-tag Ping redirect configuration, read through <see cref="PingRedirectTagConfiguration"/>.
    /// Keys are <c>EastFive.Auth.PingRedirect.{tag}.PingAuthName</c> and
    /// <c>EastFive.Auth.PingRedirect.{tag}.PingReportSetId</c>; the former
    /// <c>AffirmHealth.PDMS.PingRedirect.*</c> spellings are still read as a
    /// fallback until consumers migrate their configuration.
    /// </summary>
    public static class PingRedirectConfiguration
    {
        public const string AuthNameKeyFormat = "EastFive.Auth.PingRedirect.{0}.PingAuthName";
        public const string ReportSetIdKeyFormat = "EastFive.Auth.PingRedirect.{0}.PingReportSetId";

        public static TResult GetPingAuthName<TResult>(string tag,
            Func<string, TResult> onFound,
            Func<string, TResult> onUnspecified)
        {
            return PingRedirectTagConfiguration.For(tag)?.PingAuthName is { } authName
                && !string.IsNullOrWhiteSpace(authName)
                    ? onFound(authName)
                    : onUnspecified($"[{string.Format(AuthNameKeyFormat, tag)}] is not specified.");
        }

        public static TResult GetPingReportSetId<TResult>(string tag,
            Func<Guid, TResult> onFound,
            Func<string, TResult> onUnspecified)
        {
            return PingRedirectTagConfiguration.For(tag)?.PingReportSetId is { } reportSetId
                    ? onFound(reportSetId)
                    : onUnspecified($"[{string.Format(ReportSetIdKeyFormat, tag)}] is not specified.");
        }
    }
}
