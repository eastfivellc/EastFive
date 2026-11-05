using System.Net.Http;
using System.Reflection;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

using Xunit;
using Xunit.Sdk;

using EastFive.Api;
using EastFive.Api.Bindings;
using EastFive.Api.Core;
using EastFive.Api.Framework;
using EastFive.Api.Tests.Harness;
using EastFive.Azure.Auth;
using EastFive.Azure.OAuth.Server;
using EastFive.Azure.Persistence.AzureStorageTables;
using EastFive.Azure.Tests.OAuth;

[assembly: EastFive.Azure.Tests.Auth.RedemptionTestLoginProvider]

namespace EastFive.Azure.Tests.Auth;

/// <summary>
/// An authorization id (the <c>request_id</c> a login hands back) is exchanged for credentials
/// once, by one consumer, within <see cref="Authorization.RedemptionWindow"/> of the login
/// completing. Refresh of an already-bound session is unaffected. Every exchange runs through
/// the production dispatch pipeline against Azurite.
/// </summary>
public class AuthorizationRedemptionTests : TestSession
{
    private const string Created = "onCreated";
    private const string Updated = "onUpdated";
    private const string Found = "onFound";
    private const string Refused = "onFailure";
    private const string AlreadyExists = "onAlreadyExists";

    public AuthorizationRedemptionTests()
    {
        TestConfiguration.Ensure();
    }

    #region Client exchanges

    [Fact]
    public async Task SecondSession_CannotRedeemTheSameAuthorization()
    {
        var login = await SeedLoginAsync();
        AssertAuthorized(await PostSessionAsync(Guid.NewGuid(), login.Authorization), login);

        AssertRefused(await PostSessionAsync(Guid.NewGuid(), login.Authorization));
    }

    [Fact]
    public async Task SameSession_RebindingItsOwnAuthorization_IsIdempotent()
    {
        var login = await SeedLoginAsync();
        var sessionId = Guid.NewGuid();
        var created = AssertAuthorized(await PostSessionAsync(sessionId, login.Authorization), login);

        AssertAuthorized(await PatchSessionAsync(sessionId, login.Authorization, created.token), login, Updated);
        AssertAuthorized(await PatchSessionAsync(sessionId, login.Authorization, created.token), login, Updated);
    }

    [Fact]
    public async Task AnonymousSession_PatchedAfterAnotherRedeemed_IsRefused()
    {
        var login = await SeedLoginAsync();
        AssertAuthorized(await PostSessionAsync(Guid.NewGuid(), login.Authorization), login);

        var latecomerId = Guid.NewGuid();
        var latecomer = AssertCreated(await PostSessionAsync(latecomerId, authorization: null));
        Assert.False(latecomer.authorized);

        AssertRefused(await PatchSessionAsync(latecomerId, login.Authorization, latecomer.token));

        var stored = await latecomerId.AsRef<Session>().StorageGetAsync(
            session => session,
            () => throw new XunitException("The latecomer session was not stored."));
        Assert.False(stored.authorization.HasValueNotNull());
        Assert.Null(stored.account);
    }

    [Fact]
    public async Task AnonymousSessionThenPatch_RedeemsTheAuthorization_ForThatSessionOnly()
    {
        var login = await SeedLoginAsync();
        var sessionId = Guid.NewGuid();
        var anonymous = AssertCreated(await PostSessionAsync(sessionId, authorization: null));

        AssertAuthorized(await PatchSessionAsync(sessionId, login.Authorization, anonymous.token), login, Updated);

        AssertRefused(await PostSessionAsync(Guid.NewGuid(), login.Authorization));
    }

    [Fact]
    public async Task GetByRequestId_IsABindPoint()
    {
        var login = await SeedLoginAsync();
        var redeemerId = Guid.NewGuid();
        var redeemer = AssertAuthorized(await PostSessionAsync(redeemerId, login.Authorization), login);
        AssertAuthorized(await GetByRequestIdAsync(redeemerId, login.Authorization, redeemer.token), login, Updated);

        var latecomerId = Guid.NewGuid();
        var latecomer = AssertCreated(await PostSessionAsync(latecomerId, authorization: null));
        AssertRefused(await GetByRequestIdAsync(latecomerId, login.Authorization, latecomer.token));
    }

    [Fact]
    public async Task ConcurrentExchanges_ExactlyOneWins()
    {
        var login = await SeedLoginAsync();

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 6)
            .Select(_ => PostSessionAsync(Guid.NewGuid(), login.Authorization)));

        Assert.Single(outcomes, outcome => outcome.BranchName == Created);
        Assert.Equal(outcomes.Length - 1, outcomes.Count(outcome => outcome.BranchName == Refused));
    }

    #endregion

    #region Validity

    [Fact]
    public async Task Exchange_AfterTheRedemptionWindow_IsRefused()
    {
        var login = await SeedLoginAsync();

        using (AuthorizationRedemption.UseClock(() =>
                DateTime.UtcNow + Authorization.RedemptionWindow + TimeSpan.FromMinutes(1)))
            AssertRefused(await PostSessionAsync(Guid.NewGuid(), login.Authorization));
    }

    [Fact]
    public async Task Exchange_WithinTheRedemptionWindow_IsAccepted()
    {
        var login = await SeedLoginAsync();

        using (AuthorizationRedemption.UseClock(() =>
                DateTime.UtcNow + Authorization.RedemptionWindow - TimeSpan.FromMinutes(1)))
            AssertAuthorized(await PostSessionAsync(Guid.NewGuid(), login.Authorization), login);
    }

    [Fact]
    public async Task ExpiredAuthorization_IsRefused()
    {
        var login = await SeedLoginAsync(expired: true);

        AssertRefused(await PostSessionAsync(Guid.NewGuid(), login.Authorization));
    }

    [Fact]
    public async Task DeletedAuthorization_IsRefused()
    {
        var login = await SeedLoginAsync(deleted: DateTime.UtcNow);

        AssertRefused(await PostSessionAsync(Guid.NewGuid(), login.Authorization));
    }

    [Fact]
    public async Task UnknownAuthorization_IsRefused_RatherThanAnonymous()
    {
        AssertRefused(await PostSessionAsync(Guid.NewGuid(), Guid.NewGuid().AsRef<Authorization>()));
    }

    #endregion

    #region Refresh

    [Fact]
    public async Task Refresh_OfTheRedeemingSession_StillIssuesTokens_EvenAfterTheWindow()
    {
        var login = await SeedLoginAsync();
        var sessionId = Guid.NewGuid();
        var created = AssertAuthorized(await PostSessionAsync(sessionId, login.Authorization), login);

        AssertAuthorized(await RefreshAsync(sessionId, created.token), login, Found);
        using (AuthorizationRedemption.UseClock(() =>
                DateTime.UtcNow + Authorization.RedemptionWindow + TimeSpan.FromMinutes(1)))
            AssertAuthorized(await RefreshAsync(sessionId, created.token), login, Found);
    }

    #endregion

    #region Trusted server-side binds

    [Fact]
    public async Task TrustedCreate_RecordsTheRedemption_SoAClientExchangeIsRefused()
    {
        var login = await SeedLoginAsync();

        var trusted = await Session.CreateAsync(Application, login.Authorization.Optional());
        Assert.True(trusted.authorized);
        Assert.Equal(login.AccountId, trusted.account);

        AssertRefused(await PostSessionAsync(Guid.NewGuid(), login.Authorization));

        var rebound = await Session.CreateAsync(Application, login.Authorization.Optional());
        Assert.NotEqual(trusted.id, rebound.id);
        Assert.True(rebound.authorized);
    }

    [Fact]
    public async Task ReplayRebind_CreatesASession_AfterAClientRedeemed()
    {
        var login = await SeedLoginAsync();
        AssertAuthorized(await PostSessionAsync(Guid.NewGuid(), login.Authorization), login);

        var sessionRef = Ref<Session>.SecureRef();
        var created = default(Session?);
        await Session.CreateSessionAsync(sessionRef, login.Authorization.Optional(),
                new Session { sessionId = sessionRef, authorization = login.Authorization.Optional() },
                Application,
                rebind: true,
            (session, contentType) =>
            {
                created = session;
                return null!;
            },
            () => throw new XunitException("Replay found an existing session."),
            (key, why) => throw new XunitException($"Configuration failure {key}: {why}"),
            why => throw new XunitException($"Replay was refused: {why}"));

        Assert.True(created.HasValue);
        Assert.True(created!.Value.authorized);
        Assert.Equal(login.AccountId, created.Value.account);
        Assert.False(string.IsNullOrEmpty(created.Value.token));
    }

    #endregion

    #region OAuth authorize

    [Fact]
    public async Task OAuthResume_ASecondRequest_CannotReuseTheLoginAuthorization()
    {
        var login = await SeedLoginAsync();
        var first = await SeedAuthorizeRequestAsync();
        var second = await SeedAuthorizeRequestAsync();

        Assert.Equal("onPage", (await ResumeAsync(first, login.Authorization)).BranchName);
        Assert.Equal(OAuthAuthorizeRequest.StatusConsentPending, (await LoadAsync(first)).status);

        Assert.Equal("onPage", (await ResumeAsync(second, login.Authorization)).BranchName);
        var refused = await LoadAsync(second);
        Assert.Equal(OAuthAuthorizeRequest.StatusPending, refused.status);
        Assert.True(string.IsNullOrEmpty(refused.approvalKey));
        Assert.Null(refused.accountId);
    }

    [Fact]
    public async Task OAuthResume_AfterASessionRedeemed_IsRefused_AndViceVersa()
    {
        var sessionFirst = await SeedLoginAsync();
        AssertAuthorized(await PostSessionAsync(Guid.NewGuid(), sessionFirst.Authorization), sessionFirst);
        var request = await SeedAuthorizeRequestAsync();
        await ResumeAsync(request, sessionFirst.Authorization);
        Assert.Equal(OAuthAuthorizeRequest.StatusPending, (await LoadAsync(request)).status);

        var oauthFirst = await SeedLoginAsync();
        var otherRequest = await SeedAuthorizeRequestAsync();
        await ResumeAsync(otherRequest, oauthFirst.Authorization);
        Assert.Equal(OAuthAuthorizeRequest.StatusConsentPending, (await LoadAsync(otherRequest)).status);
        AssertRefused(await PostSessionAsync(Guid.NewGuid(), oauthFirst.Authorization));
    }

    #endregion

    #region Existing session ids

    [Fact]
    public async Task Post_ForAnExistingSessionId_MintsNoToken()
    {
        var sessionId = Guid.NewGuid();
        AssertCreated(await PostSessionAsync(sessionId, authorization: null));

        var again = await PostSessionAsync(sessionId, authorization: null);
        Assert.Equal(AlreadyExists, again.BranchName);
    }

    [Fact]
    public async Task Post_ForAnExistingSessionId_WithItsOwnAuthorization_MintsNoToken()
    {
        var login = await SeedLoginAsync();
        var sessionId = Guid.NewGuid();
        AssertAuthorized(await PostSessionAsync(sessionId, login.Authorization), login);

        var again = await PostSessionAsync(sessionId, login.Authorization);
        Assert.Equal(AlreadyExists, again.BranchName);
    }

    #endregion

    #region Assertions

    private static Session AssertCreated(ResponseBranchCapture capture, string branch = Created)
    {
        Assert.True(capture.BranchName == branch,
            $"Expected {branch}, got {capture.BranchName} ({string.Join(", ", capture.Arguments ?? Array.Empty<object?>())}; " +
            $"{capture.Response?.StatusCode} {capture.Response?.ReasonPhrase}).");
        var session = Assert.IsType<Session>(capture.Arguments![0]);
        Assert.False(string.IsNullOrEmpty(session.token));
        return session;
    }

    private static Session AssertAuthorized(ResponseBranchCapture capture, SeededLogin login,
        string branch = Created)
    {
        var session = AssertCreated(capture, branch);
        Assert.True(session.authorized);
        Assert.Equal(login.AccountId, session.account);
        return session;
    }

    private static void AssertRefused(ResponseBranchCapture capture)
    {
        Assert.True(capture.BranchName == Refused,
            $"Expected {Refused}, got {capture.BranchName} ({string.Join(", ", capture.Arguments ?? Array.Empty<object?>())}; " +
            $"{capture.Response?.StatusCode} {capture.Response?.ReasonPhrase}).");
        Assert.DoesNotContain(capture.Arguments!, argument => argument is Session);
    }

    #endregion

    #region Requests

    private static MethodInfo SessionEndpoint<TVerb>(string name) where TVerb : Attribute =>
        typeof(Session)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == name && method.GetCustomAttributes<TVerb>(true).Any());

    private Task<ResponseBranchCapture> PostSessionAsync(Guid sessionId, IRef<Authorization>? authorization)
    {
        var method = SessionEndpoint<EastFive.Api.HttpPostAttribute>(nameof(Session.CreateAsync));
        var json = authorization is null
            ? $$"""{"id":"{{sessionId}}"}"""
            : $$"""{"id":"{{sessionId}}","authorization":"{{authorization.id}}"}""";
        return DispatchRawAsync(method, BuildJsonRequest(method, json));
    }

    private Task<ResponseBranchCapture> PatchSessionAsync(Guid sessionId, IRef<Authorization> authorization,
        string token)
    {
        var method = SessionEndpoint<EastFive.Api.HttpPatchAttribute>(nameof(Session.UpdateBodyAsync));
        var request = BuildJsonRequest(method, $$"""{"authorization":"{{authorization.id}}"}""",
            new Dictionary<string, object> { [Session.SessionIdPropertyName] = sessionId });
        request.Headers["Authorization"] = new[] { "Bearer " + token };
        return DispatchRawAsync(method, request);
    }

    private Task<ResponseBranchCapture> GetByRequestIdAsync(Guid sessionId, IRef<Authorization> authorization,
        string token)
    {
        var method = SessionEndpoint<EastFive.Api.HttpGetAttribute>(nameof(Session.GetByRequestIdAsync));
        var context = new DefaultHttpContext();
        var request = context.Request;
        request.Method = HttpMethods.Get;
        request.Scheme = "https";
        request.Host = new HostString("localhost");
        request.Path = "/api/XSession";
        request.QueryString = QueryString.Create(new Dictionary<string, string?>
        {
            [Session.SessionIdPropertyName] = sessionId.ToString(),
            [EastFive.Api.Azure.AzureApplication.QueryRequestIdentfier] = authorization.id.ToString(),
        });
        request.Headers.Authorization = "Bearer " + token;
        // The endpoint takes an IProvideUrl, which only the framework instigators supply, and
        // only for a genuine ASP.NET Core request.
        return DispatchRawAsync(SessionTestApplication.Shared, method,
            new CoreHttpRequest(request, razorViewEngine: null!, CancellationToken.None));
    }

    private Task<ResponseBranchCapture> RefreshAsync(Guid sessionId, string token)
    {
        var method = SessionEndpoint<EastFive.Api.HttpGetAttribute>(nameof(Session.GetAsync));
        return DispatchRawAsync(method, OAuthRequests.Bare(HttpMethod.Get, "/api/XSession", token,
            new Dictionary<string, string> { [Session.SessionIdPropertyName] = sessionId.ToString() }));
    }

    private Task<ResponseBranchCapture> ResumeAsync(IRef<OAuthAuthorizeRequest> request,
        IRef<Authorization> authorization)
    {
        var method = typeof(OAuthAuthorize).GetMethod(nameof(OAuthAuthorize.ResumeAsync))!;
        return DispatchRawAsync(method, OAuthRequests.Bare(HttpMethod.Get, "/oauth/authorize/resume",
            query: new Dictionary<string, string>
            {
                [OAuthAuthorize.OAuthRequestParameterName] = request.id.ToString(),
                [EastFive.Api.Azure.AzureApplication.QueryRequestIdentfier] = authorization.id.ToString(),
            }));
    }

    #endregion

    #region Seeding

    public sealed record SeededLogin(IRef<Authorization> Authorization, Guid AccountId);

    /// <summary>
    /// An authorization as the IdP callback leaves it (authorized, tied to an account) plus the
    /// account mapping <see cref="Session"/> resolves its claims through.
    /// </summary>
    private static async Task<SeededLogin> SeedLoginAsync(bool expired = false, DateTime? deleted = null)
    {
        var subject = "redemption-" + Guid.NewGuid().ToString("N");
        var accountId = Guid.NewGuid();
        var methodRef = RedemptionTestLogin.MethodId.AsRef<Method>();
        var authorizationRef = Guid.NewGuid().AsRef<Authorization>();

        var authorization = new Authorization
        {
            authorizationRef = authorizationRef,
            Method = methodRef,
            parameters = new Dictionary<string, string> { [RedemptionTestLogin.SubjectParameter] = subject },
            authorized = true,
            accountIdMaybe = accountId,
            expired = expired,
            deleted = deleted,
        };
        Assert.True(await authorization.StorageCreateAsync(created => true, onAlreadyExists: () => false));

        var mapping = new AccountMapping
        {
            accountId = accountId,
            Method = methodRef,
            authorization = authorizationRef,
            accountkey = subject,
        };
        Assert.True(await mapping.StorageCreateAsync(created => true, onAlreadyExists: () => false));

        return new SeededLogin(authorizationRef, accountId);
    }

    private static async Task<IRef<OAuthAuthorizeRequest>> SeedAuthorizeRequestAsync()
    {
        var request = new OAuthAuthorizeRequest
        {
            @ref = Guid.NewGuid().AsRef<OAuthAuthorizeRequest>(),
            clientId = "redemption-client",
            redirectUri = "https://client.example.test/callback",
            codeChallenge = "challenge",
            codeChallengeMethod = OAuthServer.CodeChallengeMethodS256,
            status = OAuthAuthorizeRequest.StatusPending,
            createdOn = DateTime.UtcNow,
        };
        Assert.True(await request.StorageCreateAsync(created => true, onAlreadyExists: () => false));
        return request.@ref;
    }

    private static Task<OAuthAuthorizeRequest> LoadAsync(IRef<OAuthAuthorizeRequest> request) =>
        request.StorageGetAsync(
            stored => stored,
            () => throw new XunitException("The authorize request was not stored."));

    #endregion
}

/// <summary>
/// The shared test application plus the framework instigators a production host declares
/// (<see cref="IProvideUrl"/> among them).
/// </summary>
[FrameworkInstigators]
[CaptureResponseBranches]
[StandardStringBindings]
[StandardJTokenBindings]
public sealed class SessionTestApplication : HttpApplication
{
    private static readonly Lazy<SessionTestApplication> instance = new(() =>
        new SessionTestApplication(new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build()));

    public SessionTestApplication(IConfiguration configuration) : base(configuration) { }

    public static SessionTestApplication Shared => instance.Value;
}

/// <summary>Registers <see cref="RedemptionTestLogin"/> through the domain attribute scan.</summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class RedemptionTestLoginProviderAttribute : Attribute, IProvideLoginProvider
{
    public Task<TResult> ProvideLoginProviderAsync<TResult>(
        Func<IProvideLogin, TResult> onLoaded,
        Func<string, TResult> onNotAvailable) =>
        Task.FromResult(onLoaded(new RedemptionTestLogin()));
}

/// <summary>A login method whose external user key is a parameter stored on the authorization.</summary>
public sealed class RedemptionTestLogin : IProvideLogin, IProvideLoginUnlisted
{
    public static readonly Guid MethodId = new("5f2b7d4e-8a61-4c3b-9e0d-1a7c6b2f9e31");
    public const string SubjectParameter = "subject";

    public Type CallbackController => typeof(RedemptionTestLogin);

    public Guid Id => MethodId;

    public string Method => "RedemptionTestLogin";

    public Uri GetLoginUrl(Guid state, Uri responseControllerLocation, Func<Type, Uri> controllerToLocation) =>
        new($"https://login.example.test/?state={state}");

    public Uri GetLogoutUrl(Guid state, Uri responseControllerLocation, Func<Type, Uri> controllerToLocation) =>
        new($"https://login.example.test/logout?state={state}");

    public Uri GetSignupUrl(Guid state, Uri responseControllerLocation, Func<Type, Uri> controllerToLocation) =>
        new($"https://login.example.test/signup?state={state}");

    public Task<TResult> RedeemTokenAsync<TResult>(IDictionary<string, string> responseParams,
        Func<IDictionary<string, string>, TResult> onSuccess,
        Func<Guid?, IDictionary<string, string>, TResult> onNotAuthenticated,
        Func<string, TResult> onInvalidToken,
        Func<string, TResult> onCouldNotConnect,
        Func<string, TResult> onUnspecifiedConfiguration,
        Func<string, TResult> onFailure) =>
        Task.FromResult(onSuccess(responseParams));

    public TResult ParseCredentailParameters<TResult>(IDictionary<string, string> responseParams,
        Func<string, IRefOptional<Authorization>, TResult> onSuccess,
        Func<string, TResult> onFailure) =>
        responseParams is not null && responseParams.TryGetValue(SubjectParameter, out var subject)
            ? onSuccess(subject, RefOptional<Authorization>.Empty())
            : onFailure("No subject.");
}
