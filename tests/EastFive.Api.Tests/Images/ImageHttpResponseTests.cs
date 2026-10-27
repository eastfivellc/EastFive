using System.Net;

using Microsoft.AspNetCore.Http;

using Xunit;

using EastFive.Api.Tests.Harness;

using static EastFive.Api.Tests.Images.ImageFixtures;

namespace EastFive.Api.Tests.Images;

/// <summary>
/// The image response delegates as the framework instigates them: status, Content-Type, a body
/// that decodes, and the requested resize.
/// </summary>
public class ImageHttpResponseTests : TestSession
{
    private static async Task<(HttpStatusCode Status, string? ContentType, byte[] Body)> RenderAsync(IHttpResponse response)
    {
        var context = new DefaultHttpContext();
        using var body = new MemoryStream();
        context.Response.Body = body;

        await response.WriteResponseAsync(context);

        return ((HttpStatusCode)context.Response.StatusCode, context.Response.ContentType, body.ToArray());
    }

    /// <summary>
    /// Builds the probe's response delegate with the instigator on the delegate TYPE, as the
    /// framework does, and invokes the probe with it. Not <c>DispatchRawAsync</c>: the harness
    /// replaces response delegates with recording stubs, which would never render an image.
    /// </summary>
    private Task<IHttpResponse> DispatchAsync(Type probe)
    {
        var method = probe.GetMethod("Fetch")!;
        var parameter = method.GetParameters().Single();
        var instigator = parameter.ParameterType.GetCustomAttributes(inherit: true)
            .OfType<HttpFuncDelegateAttribute>()
            .Single();
        return instigator.InstigateInternal(Application, BuildJsonRequest(method, "{}"), parameter,
            responseDelegate => Task.FromResult((IHttpResponse)method.Invoke(null, [responseDelegate])!));
    }

    [Fact]
    public async Task ImageObjectResponse_ResizesAndEncodesTheRequestedType()
    {
        var (status, contentType, body) = await RenderAsync(await DispatchAsync(typeof(ImageObjectResponseProbe)));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("image/png", contentType);
        Assert.Equal((4, 2, "image/png"), Decode(body));
    }

    [Fact]
    public async Task ImageRawResponse_WithASize_Resizes()
    {
        var (status, _, body) = await RenderAsync(await DispatchAsync(typeof(ImageRawResizedResponseProbe)));

        Assert.Equal(HttpStatusCode.OK, status);
        var (width, height, _) = Decode(body);
        Assert.Equal((4, 2), (width, height));
    }

    [Fact]
    public async Task ImageRawResponse_WithoutASize_PassesTheBytesThrough()
    {
        var (status, contentType, body) = await RenderAsync(await DispatchAsync(typeof(ImageRawResponseProbe)));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("image/png", contentType);
        Assert.Equal(HalvesPng(), body);
    }
}

[FunctionViewController(Route = "image-object-response-probe")]
public static class ImageObjectResponseProbe
{
    [HttpPost]
    public static IHttpResponse Fetch(ImageSharpResponse onImage)
        => onImage(Halves(), width: 4, contentType: "image/png");
}

[FunctionViewController(Route = "image-raw-resized-response-probe")]
public static class ImageRawResizedResponseProbe
{
    [HttpPost]
    public static IHttpResponse Fetch(ImageRawResponse onImage)
        => onImage(HalvesPng(), width: 4, contentType: "image/png");
}

[FunctionViewController(Route = "image-raw-response-probe")]
public static class ImageRawResponseProbe
{
    [HttpPost]
    public static IHttpResponse Fetch(ImageRawResponse onImage)
        => onImage(HalvesPng(), contentType: "image/png");
}
