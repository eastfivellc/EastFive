using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

using EastFive.Api.Binding;
using EastFive.Api.Tests.Harness;

namespace EastFive.Api.Tests;

public class JsonTokenResponseTests : TestSession
{
    [Fact]
    public void JsonObjectUsesNativeReadAndWriteWithCoreConverterRegistered()
    {
        const string json = """{"schema":{"type":"object","required":["name"]},"values":[1,true,null,"text"]}""";
        var converter = new EastFive.Serialization.Json.Converter();

        Assert.False(converter.CanConvert(typeof(JObject)));
        var value = JsonConvert.DeserializeObject<JObject>(json, converter);
        var written = JsonConvert.SerializeObject(value, converter);

        Assert.True(JToken.DeepEquals(JToken.Parse(json), JToken.Parse(written)));
    }

    [Theory]
    [InlineData("""{"content_schema":{"type":"object","properties":{"enabled":{"type":"boolean","default":true},"count":{"type":"integer","default":3},"name":{"type":"string","default":"a \"quoted\" value"}},"required":["enabled"],"additionalProperties":false,"example":null}}""")]
    [InlineData("""{"facts":{"schema":{"type":"object","properties":{"name":{"type":"string"}}},"enabled":true,"count":3,"missing":null}}""")]
    [InlineData("""{"choices":[{"type":"string","enum":["one","two"]},[1,false,null],{}]}""")]
    public async Task JsonTokensInResponseSerializeLosslessly(string json)
    {
        var method = typeof(JsonTokenResponseProbe).GetMethod(nameof(JsonTokenResponseProbe.Echo))!;
        var request = BuildJsonRequest(method, json);

        var capture = await DispatchRawAsync(method, request);

        Assert.NotNull(capture.Response);
        Assert.Equal(HttpStatusCode.OK, capture.Response.StatusCode);
        using var stream = new MemoryStream();
        await capture.Response.WriteResponseAsync(stream);

        var actual = JToken.Parse(Encoding.UTF8.GetString(stream.ToArray()));
        Assert.True(JToken.DeepEquals(JToken.Parse(json), actual),
            $"Response JSON changed during serialization: {actual}");
    }
}

[FunctionViewController(Route = "json-token-response-probe")]
public static class JsonTokenResponseProbe
{
    public sealed class Document
    {
        [JsonProperty("content_schema", NullValueHandling = NullValueHandling.Ignore)]
        public JObject? ContentSchema { get; set; }

        [JsonProperty("facts", NullValueHandling = NullValueHandling.Ignore)]
        public System.Collections.Generic.Dictionary<string, object?>? Facts { get; set; }

        [JsonProperty("choices", NullValueHandling = NullValueHandling.Ignore)]
        public JArray? Choices { get; set; }
    }

    [HttpPost]
    public static IHttpResponse Echo([BodyText] string body, IHttpRequest request)
        => new JsonHttpResponse(request, HttpStatusCode.OK,
            JsonConvert.DeserializeObject<Document>(body));
}