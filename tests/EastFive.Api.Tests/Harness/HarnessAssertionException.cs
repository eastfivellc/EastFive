using System;

namespace EastFive.Api.Tests.Harness;

/// <summary>
/// Single throw type used by branch assertion helpers. Test frameworks
/// surface the throw as an ordinary failure; the message follows the
/// canonical <c>"expected X, got Y"</c> shape.
/// </summary>
public sealed class HarnessAssertionException : Exception
{
    public HarnessAssertionException(string expected, string actual)
        : base($"expected {expected}, got {actual}")
    {
        this.Expected = expected;
        this.Actual = actual;
    }

    public string Expected { get; }
    public string Actual { get; }
}
