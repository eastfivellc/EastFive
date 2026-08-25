using System;

using EastFive.Web.Configuration;

namespace EastFive.Azure.Auth
{
    /// <summary>
    /// Per-tag Ping redirect configuration. Keys are
    /// <c>EastFive.Auth.PingRedirect.{tag}.PingAuthName</c> and
    /// <c>EastFive.Auth.PingRedirect.{tag}.PingReportSetId</c>; the former
    /// <c>AffirmHealth.PDMS.PingRedirect.*</c> spellings are still read as a
    /// fallback until consumers migrate their configuration.
    /// </summary>
    public static class PingRedirectConfiguration
    {
        public const string AuthNameKeyFormat = "EastFive.Auth.PingRedirect.{0}.PingAuthName";
        public const string ReportSetIdKeyFormat = "EastFive.Auth.PingRedirect.{0}.PingReportSetId";
        private const string LegacyAuthNameKeyFormat = "AffirmHealth.PDMS.PingRedirect.{0}.PingAuthName";
        private const string LegacyReportSetIdKeyFormat = "AffirmHealth.PDMS.PingRedirect.{0}.PingReportSetId";

        public static TResult GetPingAuthName<TResult>(string tag,
            Func<string, TResult> onFound,
            Func<string, TResult> onUnspecified)
        {
            return Settings.GetString(string.Format(AuthNameKeyFormat, tag),
                onFound,
                why => Settings.GetString(string.Format(LegacyAuthNameKeyFormat, tag),
                    onFound,
                    _ => onUnspecified(why)));
        }

        public static TResult GetPingReportSetId<TResult>(string tag,
            Func<Guid, TResult> onFound,
            Func<string, TResult> onUnspecified)
        {
            return Settings.GetGuid(string.Format(ReportSetIdKeyFormat, tag),
                onFound,
                why => Settings.GetGuid(string.Format(LegacyReportSetIdKeyFormat, tag),
                    onFound,
                    _ => onUnspecified(why)));
        }
    }
}
