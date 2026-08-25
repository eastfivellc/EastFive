using System;
using System.Threading.Tasks;

using Xunit;

using EastFive.Api.Tests.Harness;
using EastFive.Api.Tests.Probes;

namespace EastFive.Api.Tests;

/// <summary>
/// Regression: [BodyText] must receive the raw request text for
/// application/json bodies. JsonEnvelope previously discarded the raw body
/// text, so [BodyText] never selected for JSON requests and dispatch
/// answered a bare 501 (XML/raw content types worked).
/// </summary>
public class BodyTextJsonTests : TestSession
{
    [Fact]
    public async Task JsonBodyReachesBodyTextParameterAsRawText()
    {
        var method = typeof(BodyTextProbe).GetMethod(nameof(BodyTextProbe.Receive))!;
        var json = /*lang=json*/ """{"kind":"webhook","value":42}""";
        var request = TestSession.BuildJsonRequest(method, json);

        var capture = await DispatchRawAsync(method, request);

        Assert.Equal("onReceived", capture.BranchName);
        Assert.True(capture.TryGet("onReceived", out var args));
        var received = Assert.IsType<string>(args[0]);
        Assert.Equal(json, received);
    }
}
