using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Xunit;

using EastFive.Api;
using EastFive.Azure.Auth;
using EastFive.Azure.Tests.OAuth;
using EastFive.Persistence.Azure.StorageTables.Driver;

namespace EastFive.Azure.Tests.Auth;

/// <summary>
/// The ETag member is storage bookkeeping: it is neither sent in a response nor stored as a
/// property of the row. The expected JSON is what each type serialized to before it had one.
/// The response writer leaves out untagged members on its own; for a type that is not a
/// <c>[DataContract]</c>, <c>[JsonIgnore]</c> is what keeps the member out of plain Newtonsoft.
/// </summary>
public class ETagMemberShapeTests
{
    private static readonly Guid SessionId = new("0c3f9a52-6d1e-4b7a-8f20-5e94d1c3b7a6");
    private static readonly Guid AuthorizationId = new("8e1d4b27-3a96-4c50-b1f8-27d6e9a0c413");
    private static readonly Guid AccountId = new("f47a2c19-0b58-4e3d-9a61-c8b5d2e7f034");

    private static Session Sample() => new()
    {
        sessionId = SessionId.AsRef<Session>(),
        eTag = "W/\"datetime'2026-11-21T00%3A00%3A00Z'\"",
        expires = new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc),
        authorization = AuthorizationId.AsRef<Authorization>().Optional(),
        account = AccountId,
        authorized = true,
        token = "token",
        refreshToken = "refresh",
    };

    [Fact]
    public async Task SessionResponse_IsTheSameJsonAsBeforeTheMember()
    {
        var request = OAuthRequests.Bare(HttpMethod.Get, "/api/XSession");
        using var body = new MemoryStream();
        await Api.JsonHttpResponse.WriteResponseAsync(body, Sample(), request, new UTF8Encoding(false));

        var expected = JToken.Parse($$"""
            {
              "id": "{{SessionId}}",
              "expires": "2026-11-21T12:00:00Z",
              "authorization": "{{AuthorizationId}}",
              "account": "{{AccountId}}",
              "authorized": true,
              "header_name": "Authorization",
              "token": "token",
              "refresh_token": "refresh"
            }
            """);
        var actual = JToken.Parse(Encoding.UTF8.GetString(body.ToArray()));
        Assert.True(JToken.DeepEquals(expected, actual), actual.ToString());
    }

    [Fact]
    public void LoginAccount_PlainNewtonsoft_IsTheSameJsonAsBeforeTheMember()
    {
        var accountId = new Guid("2b6e8d14-5f07-4a93-8c21-d9e4a0b7f356");
        var account = new EastFive.Azure.Login.Account
        {
            accountRef = accountId.AsRef<EastFive.Azure.Login.Account>(),
            eTag = "W/\"datetime'2026-11-21T00%3A00%3A00Z'\"",
            userIdentification = "user",
            password = "hash",
        };

        var expected = JToken.Parse($$"""
            {
              "id": { "id": "{{accountId}}" },
              "user_identification": "user",
              "password": "hash"
            }
            """);
        var actual = JToken.Parse(JsonConvert.SerializeObject(account));
        Assert.True(JToken.DeepEquals(expected, actual), actual.ToString());
    }

    [Fact]
    public void SessionRow_DoesNotStoreTheMember()
    {
        var stored = AzureTableDriverDynamic.GetEntity(Sample()).WriteEntity(null);

        Assert.DoesNotContain(stored.Keys, key => key.Contains("tag", StringComparison.OrdinalIgnoreCase));
    }
}
