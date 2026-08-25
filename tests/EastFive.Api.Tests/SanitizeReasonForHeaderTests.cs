using System;

using Xunit;

using EastFive.Api;

namespace EastFive.Api.Tests;

/// <summary>
/// Regression: reason strings travel as HTTP headers (x-reason) and the
/// reason phrase; Kestrel 500s on any non-ASCII or control character. The
/// sanitizer must transliterate common typography and strip the rest —
/// including the multi-line messages JWT validation produces (IDX10503
/// "...\nExceptions caught:\n...").
/// </summary>
public class SanitizeReasonForHeaderTests
{
    [Fact]
    public void EmDashAndEllipsisTransliterateToAscii()
    {
        var sanitized = HttpResponse.SanitizeReasonForHeader("state invalid — retry… now");
        AssertHeaderSafe(sanitized);
        Assert.Contains("state invalid", sanitized);
        Assert.Contains("retry", sanitized);
    }

    [Fact]
    public void MultiLineJwtValidationMessageBecomesSingleLine()
    {
        var reason = "IDX10503: Signature validation failed.\nExceptions caught:\n 'System.Text'";
        var sanitized = HttpResponse.SanitizeReasonForHeader(reason);
        AssertHeaderSafe(sanitized);
        Assert.Contains("IDX10503", sanitized);
    }

    [Fact]
    public void CurlyQuotesAndNbspTransliterate()
    {
        var sanitized = HttpResponse.SanitizeReasonForHeader("\u201cquoted\u201d\u00a0value \u2018x\u2019");
        AssertHeaderSafe(sanitized);
        Assert.Contains("quoted", sanitized);
    }

    private static void AssertHeaderSafe(string sanitized)
    {
        Assert.NotNull(sanitized);
        foreach (var c in sanitized)
        {
            Assert.InRange((int)c, 0x20, 0x7e);
        }
    }
}
