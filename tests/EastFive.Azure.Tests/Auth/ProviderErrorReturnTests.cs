using System.Text;
using System.Web;

using Microsoft.AspNetCore.Http;

using Xunit;

using EastFive.Api;
using EastFive.Api.Core;
using EastFive.Api.Tests.Harness;
using EastFive.Azure.Auth;
using EastFive.Azure.Auth.Microsoft;
using EastFive.Azure.Persistence.AzureStorageTables;

namespace EastFive.Azure.Tests.Auth;

/// <summary>
/// An identity provider's ERROR return (no code): Microsoft's "Return to the application without
/// granting consent" on "Need admin approval", or a cancel. The redirect endpoints required a
/// code, so such a return matched no method and the server answered 501. It now goes back to the
/// app through <see cref="IProvideRejectionRedirect"/>, or to a readable page.
/// </summary>
public class ProviderErrorReturnTests : TestSession
{
    private static readonly IHttpRequest Callback =
        new EastFive.Api.HttpRequest(new Uri("https://app.example.com/auth/MicrosoftRedirect"));

    public ProviderErrorReturnTests()
    {
        TestConfiguration.Ensure();
    }

    private static async Task<Authorization> SeedAuthorizationAsync(Uri? returnLocation)
    {
        var authorization = new Authorization
        {
            authorizationRef = Guid.NewGuid().AsRef<Authorization>(),
            LocationAuthenticationReturn = returnLocation!,
        };
        Assert.True(await authorization.StorageCreateAsync(created => true, onAlreadyExists: () => false));
        return authorization;
    }

    private static Task<string> OutcomeAsync(string? state, string error, IProvideAccountInformation provider) =>
        Redirection.ProviderErrorAsync(state!, error, provider, Callback,
            uri => $"redirect:{uri.AbsoluteUri}",
            message => $"default:{message}");

    [Fact]
    public async Task AnErrorReturn_GoesBackToTheApp_WithTheProvidersReasonCode()
    {
        var authorization = await SeedAuthorizationAsync(new Uri("https://app.example.com/login"));
        var provider = new MapsProviderErrors();

        var outcome = await OutcomeAsync(authorization.id.ToString("N"), "consent_required", provider);

        Assert.StartsWith("redirect:https://app.example.com/login", outcome);
        Assert.Equal("approval_needed",
            HttpUtility.ParseQueryString(new Uri(outcome["redirect:".Length..]).Query)["sign_in"]);
        Assert.Equal(Redirection.ProviderErrorReasonPrefix + "consent_required", provider.SeenWhy);
    }

    [Fact]
    public async Task TheAuthorizationStaysUnauthorized()
    {
        var authorization = await SeedAuthorizationAsync(new Uri("https://app.example.com/login"));

        await OutcomeAsync(authorization.id.ToString("N"), "access_denied", new MapsProviderErrors());

        var authorized = await authorization.authorizationRef.StorageGetAsync(a => (bool?)a.authorized, () => null);
        Assert.False(authorized);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData(null)]
    public async Task AnUnknownState_GetsAReadablePage(string? state)
    {
        var outcome = await OutcomeAsync(state, "access_denied", new MapsProviderErrors());

        Assert.Equal("default:Sign-in did not complete (access_denied). Return to the application to try again.", outcome);
    }

    [Fact]
    public async Task AProviderWithoutTheHook_GetsAReadablePage()
    {
        var authorization = await SeedAuthorizationAsync(new Uri("https://app.example.com/login"));

        var outcome = await OutcomeAsync(authorization.id.ToString("N"), "access_denied", new NoHook());

        Assert.StartsWith("default:Sign-in did not complete (access_denied)", outcome);
    }

    [Fact]
    public async Task TheErrorCodeIsReducedToASafeToken()
    {
        var outcome = await OutcomeAsync(null, "Access_Denied<script>alert(1)</script>", new NoHook());

        Assert.Equal("default:Sign-in did not complete (access_deniedscriptalert1script). Return to the application to try again.", outcome);
    }

    [Fact]
    public void TheReasonParsesBackToTheErrorCode()
    {
        Assert.True(Redirection.TryParseProviderError(Redirection.ProviderErrorReason("interaction_required"), out var error));
        Assert.Equal("interaction_required", error);
        Assert.False(Redirection.TryParseProviderError("Your account request is awaiting administrator approval.", out _));
    }

    // ---- Through the real form envelope ------------------------------------------------------

    private static IHttpRequest FormPost(params (string key, string value)[] fields)
    {
        var bytes = Encoding.UTF8.GetBytes(string.Join("&", fields.Select(f =>
            $"{Uri.EscapeDataString(f.key)}={Uri.EscapeDataString(f.value)}")));
        var request = new DefaultHttpContext().Request;
        request.Method = "POST";
        request.Scheme = "https";
        request.Host = new HostString("app.example.com");
        request.Path = "/auth/MicrosoftRedirect";
        request.ContentType = "application/x-www-form-urlencoded";
        request.ContentLength = bytes.Length;
        request.Body = new MemoryStream(bytes);
        return new CoreHttpRequest(request, razorViewEngine: null!, CancellationToken.None);
    }

    private static System.Reflection.MethodInfo Method(string name) =>
        typeof(MicrosoftRedirect).GetMethod(name)!;

    /// <summary>What Microsoft posts from "Need admin approval" → "Return to the application".</summary>
    private static IHttpRequest MicrosoftErrorForm(string state) => FormPost(
        ("error", "consent_required"),
        ("error_description", "AADSTS90094: The grant requires admin permission."),
        ("error_uri", "https://login.microsoftonline.com/error?code=90094"),
        ("state", state));

    [Fact]
    public async Task MicrosoftsErrorForm_BindsTheErrorHandler()
    {
        var authorization = await SeedAuthorizationAsync(new Uri("https://app.example.com/login"));

        var capture = await DispatchRawAsync(Method(nameof(MicrosoftRedirect.ErrorPost)),
            MicrosoftErrorForm(authorization.id.ToString("N")));

        Assert.True(capture.BranchName is "onRedirectResponse" or "onFailure",
            $"branch={capture.BranchName ?? "none"} status={capture.Response?.StatusCode} " +
            $"reason={string.Join(",", capture.Response?.Headers?.Where(h => h.Key.Contains("reason", StringComparison.OrdinalIgnoreCase)).SelectMany(h => h.Value) ?? Array.Empty<string>())}");
        Assert.DoesNotContain("AADSTS90094", capture.Response?.ToString() ?? string.Empty);
    }

    [Fact]
    public async Task MicrosoftsErrorForm_DoesNotReachTheCodeHandler()
    {
        var capture = await DispatchRawAsync(Method(nameof(MicrosoftRedirect.RedirectedPost)),
            MicrosoftErrorForm(Guid.NewGuid().ToString("N")));

        Assert.Null(capture.BranchName);
    }

    [Fact]
    public async Task ASuccessfulForm_DoesNotReachTheErrorHandler()
    {
        var capture = await DispatchRawAsync(Method(nameof(MicrosoftRedirect.ErrorPost)),
            FormPost(("code", "a-code"), ("state", Guid.NewGuid().ToString("N")), ("session_state", "s")));

        Assert.Null(capture.BranchName);
    }

    private class NoHook : IProvideAccountInformation
    {
        public Task<TResult> FindOrCreateAccountByMethodAndKeyAsync<TResult>(Method authenticationMethod,
                string externalAccountKey, Authorization authorization, IDictionary<string, string> extraParams,
                IProvideLogin loginProvider, IHttpRequest request, Func<IAccount, IAccount> populateAccount,
            Func<Guid, IDictionary<string, string>, TResult> onAccountReady,
            Func<Uri, Guid, IDictionary<string, string>, TResult> onInterceptProcess,
            Func<string, TResult> onReject) => throw new NotSupportedException();

        public Task<TResult> FindAccountByMethodAndKeyAsync<TResult>(Method authenticationMethod,
                string externalAccountKey, Authorization authorization,
            Func<Guid, IDictionary<string, string>, TResult> onAccountFound,
            Func<TResult> onReject) => throw new NotSupportedException();
    }

    /// <summary>Maps a provider error to <c>sign_in=approval_needed</c>; records the why.</summary>
    private sealed class MapsProviderErrors : NoHook, IProvideRejectionRedirect
    {
        public string? SeenWhy { get; private set; }

        public TResult DescribeRejection<TResult>(string why, Uri returnLocation, IHttpRequest request,
            Func<Uri, TResult> onRedirect,
            Func<TResult> onKeepDefault)
        {
            SeenWhy = why;
            return Redirection.TryParseProviderError(why, out _)
                ? onRedirect(returnLocation.SetQueryParam("sign_in", "approval_needed"))
                : onKeepDefault();
        }
    }
}
