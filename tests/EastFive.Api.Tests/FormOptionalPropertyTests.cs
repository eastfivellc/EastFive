using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

using Xunit;

using EastFive.Api.Core;
using EastFive.Api.Tests.Harness;
using EastFive.Api.Tests.Probes;

namespace EastFive.Api.Tests;

/// <summary>
/// Regression: [PropertyOptional] through the REAL URL-encoded form envelope.
/// An omitted optional field must bind its default, as it does for JSON; the
/// form envelope used to fulfill a keyed requirement from the whole form, so
/// the converter later failed with "Key not found" and the request 400'd.
/// Microsoft's form_post sign-in redirect omits most of the fields
/// MicrosoftRedirect declares optional, so every Microsoft sign-in failed.
/// </summary>
public class FormOptionalPropertyTests : TestSession
{
    private static System.Reflection.MethodInfo Method =>
        typeof(FormOptionalProbe).GetMethod(nameof(FormOptionalProbe.Receive))!;

    private static IHttpRequest BuildFormRequest(params (string key, string value)[] fields)
    {
        var body = string.Join("&", fields.Select(f =>
            $"{Uri.EscapeDataString(f.key)}={Uri.EscapeDataString(f.value)}"));
        var bytes = Encoding.UTF8.GetBytes(body);

        var context = new DefaultHttpContext();
        var request = context.Request;
        request.Method = "POST";
        request.Scheme = "http";
        request.Host = new HostString("localhost");
        request.Path = "/api/form-optional-probe";
        request.ContentType = "application/x-www-form-urlencoded";
        request.ContentLength = bytes.Length;
        request.Body = new MemoryStream(bytes);
        return new CoreHttpRequest(request, razorViewEngine: null!, CancellationToken.None);
    }

    [Fact]
    public async Task OmittedOptionalFieldsBindDefaults()
    {
        var capture = await DispatchRawAsync(Method, BuildFormRequest(("state", "s1")));

        Assert.Equal("onReceived", capture.BranchName);
        Assert.True(capture.TryGet("onReceived", out var args));
        Assert.Equal("s1|null|unspecified", Assert.IsType<string>(args[0]));
    }

    [Fact]
    public async Task PresentOptionalFieldsBindValues()
    {
        var capture = await DispatchRawAsync(Method,
            BuildFormRequest(("state", "s1"), ("id_token", "t1"), ("session_state", "x1")));

        Assert.Equal("onReceived", capture.BranchName);
        Assert.True(capture.TryGet("onReceived", out var args));
        Assert.Equal("s1|t1|x1", Assert.IsType<string>(args[0]));
    }

    [Fact]
    public async Task OmittedRequiredFieldStillFails()
    {
        var capture = await DispatchRawAsync(Method, BuildFormRequest(("id_token", "t1")));

        Assert.False(capture.TryGet("onReceived", out _));
        Assert.NotNull(capture.Response);
        Assert.NotEqual(HttpStatusCode.OK, capture.Response!.StatusCode);
    }
}
