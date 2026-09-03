using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Xunit;

using EastFive.Api.Tests.Harness;
using EastFive.Api.Tests.Probes;

namespace EastFive.Api.Tests;

/// <summary>
/// Regression: binding <c>object</c>-typed targets through the REAL JSON
/// envelope. <c>[Body] Property&lt;IDictionary&lt;string, object&gt;&gt;</c>
/// makes DictionaryBinder bind each value as <c>object</c>; with nothing
/// claiming <c>typeof(object)</c> that fell to PocoBinder, which supplies only
/// onNull/onObject to GetValue — so a JSON string produced
/// <c>WrongSourceType("object", "String")</c> and the request 400'd
/// (`values.recipientName` in the live interaction-host failure). ObjectBinder
/// must box every JSON native as-is, bind a nested object as
/// <c>IDictionary&lt;string, object&gt;</c>, and a nested array as
/// <c>object[]</c>.
/// </summary>
public class ObjectBindingTests : TestSession
{
    private static System.Reflection.MethodInfo Method =>
        typeof(ObjectBindingProbe).GetMethod(nameof(ObjectBindingProbe.Receive))!;

    [Fact]
    public async Task ObjectTargetsBindFromAnyJsonNative()
    {
        var request = TestSession.BuildJsonRequest(Method, /*lang=json*/
            """{"values":{"s":"x","n":1,"f":1.5,"b":true,"z":null,"o":{"k":"v"},"a":[1,"two"]},"single":"y"}""");

        var capture = await DispatchRawAsync(Method, request);

        Assert.Equal("onReceived", capture.BranchName);
        Assert.True(ObjectBindingProbe.LastValuesSpecified);
        var values = ObjectBindingProbe.LastValues;
        Assert.NotNull(values);

        Assert.Equal("x", Assert.IsType<string>(values["s"]));
        Assert.Equal(1L, Assert.IsType<long>(values["n"]));
        Assert.Equal(1.5, Assert.IsType<double>(values["f"]));
        Assert.True(Assert.IsType<bool>(values["b"]));
        Assert.Null(values["z"]);

        var nested = Assert.IsAssignableFrom<IDictionary<string, object>>(values["o"]);
        Assert.Equal("v", Assert.IsType<string>(nested["k"]));

        var array = Assert.IsType<object[]>(values["a"]);
        Assert.Equal(new object[] { 1L, "two" }, array);

        Assert.Equal("y", Assert.IsType<string>(ObjectBindingProbe.LastSingle));
    }
}
