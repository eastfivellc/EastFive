using System;
using System.Threading.Tasks;

using Xunit;

using EastFive.Api.Tests.Harness;
using EastFive.Api.Tests.Probes;

namespace EastFive.Api.Tests;

/// <summary>
/// Regression: PATCH binding of Property&lt;IRefOptional&lt;T&gt;&gt; through the
/// REAL JSON envelope (production dispatch source). An omitted key must bind
/// unspecified — the binder previously mapped absent→empty optional and
/// reported specified=true, wiping the stored ref on every PATCH that
/// omitted the key. An explicit null must bind specified+empty (a
/// deliberate clear).
/// </summary>
public class PropertyPatchOmittedKeyTests : TestSession
{
    private static System.Reflection.MethodInfo Method =>
        typeof(PropertyPatchProbe).GetMethod(nameof(PropertyPatchProbe.Update))!;

    [Fact]
    public async Task OmittedKeyBindsUnspecified()
    {
        var request = TestSession.BuildJsonRequest(Method, /*lang=json*/ """{"name":"kept"}""");

        var capture = await DispatchRawAsync(Method, request);

        Assert.Equal("onResult", capture.BranchName);
        Assert.True(capture.TryGet("onResult", out var args));
        Assert.Equal("unspecified", Assert.IsType<string>(args[0]));
    }

    [Fact]
    public async Task ExplicitNullBindsSpecifiedEmpty()
    {
        var request = TestSession.BuildJsonRequest(Method, /*lang=json*/ """{"linked":null}""");

        var capture = await DispatchRawAsync(Method, request);

        Assert.Equal("onResult", capture.BranchName);
        Assert.True(capture.TryGet("onResult", out var args));
        Assert.Equal("specified:empty", Assert.IsType<string>(args[0]));
    }

    [Fact]
    public async Task ExplicitGuidBindsSpecifiedRef()
    {
        var id = Guid.NewGuid();
        var request = TestSession.BuildJsonRequest(Method, $$"""{"linked":"{{id}}"}""");

        var capture = await DispatchRawAsync(Method, request);

        Assert.Equal("onResult", capture.BranchName);
        Assert.True(capture.TryGet("onResult", out var args));
        Assert.Equal($"specified:{id}", Assert.IsType<string>(args[0]));
    }
}
