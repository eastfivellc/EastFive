using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Xunit;

using EastFive.Api;
using EastFive.Azure.Auth;

namespace EastFive.Azure.Tests.Auth;

/// <summary>
/// <see cref="Redirection.RejectionRedirect"/>: when the account provider refuses a sign-in and
/// also implements <see cref="IProvideRejectionRedirect"/>, the person goes back to the
/// authorization's return location with the provider's reason parameters, instead of the bare
/// HTML reason on the provider's redirect endpoint. Anything else keeps today's response.
/// </summary>
public class RejectionRedirectTests
{
    private const string Why = "Your account has been requested and is awaiting administrator approval.";

    private static Authorization AuthorizationReturningTo(Uri? location) => new()
    {
        authorizationRef = Guid.NewGuid().AsRef<Authorization>(),
        LocationAuthenticationReturn = location!,
    };

    private static readonly IHttpRequest Request =
        new EastFive.Api.HttpRequest(new Uri("https://app.example.com/auth/TestRedirect"));

    private static string Outcome(Authorization authorization, IProvideAccountInformation provider) =>
        Redirection.RejectionRedirect(authorization, Why, provider, Request,
            uri => uri.AbsoluteUri,
            () => "default");

    [Fact]
    public void AProviderThatDescribesTheRejection_RedirectsToTheReturnLocationWithItsParameters()
    {
        var authorization = AuthorizationReturningTo(new Uri("https://app.example.com/login?next=%2Fhome"));

        var outcome = Outcome(authorization, new Describing(("sign_in", "pending")));

        var uri = new Uri(outcome);
        Assert.Equal("app.example.com", uri.Host);
        Assert.Equal("/login", uri.AbsolutePath);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        Assert.Equal("pending", query["sign_in"]);
        Assert.Equal("/home", query["next"]);
    }

    [Fact]
    public void TheRedirectNeverCarriesTheAuthorizationId()
    {
        var authorization = AuthorizationReturningTo(new Uri("https://app.example.com/login"));

        var outcome = Outcome(authorization, new Describing(("sign_in", "pending")));

        Assert.DoesNotContain("request_id", outcome);
        Assert.DoesNotContain(authorization.authorizationRef.id.ToString(), outcome);
    }

    [Fact]
    public void TheProviderIsHandedTheReasonTheReturnLocationAndTheRequest()
    {
        var location = new Uri("https://app.example.com/login");
        var provider = new Describing(("sign_in", "pending"));

        Outcome(AuthorizationReturningTo(location), provider);

        Assert.Equal(Why, provider.SeenWhy);
        Assert.Equal(location, provider.SeenReturnLocation);
        Assert.Same(Request, provider.SeenRequest);
    }

    [Fact]
    public void AProviderWithoutTheHook_KeepsTheDefaultResponse()
    {
        var authorization = AuthorizationReturningTo(new Uri("https://app.example.com/login"));

        Assert.Equal("default", Outcome(authorization, new Plain()));
    }

    [Fact]
    public void AProviderThatDeclines_KeepsTheDefaultResponse()
    {
        var authorization = AuthorizationReturningTo(new Uri("https://app.example.com/login"));

        Assert.Equal("default", Outcome(authorization, new Describing()));
    }

    [Fact]
    public void NoReturnLocation_KeepsTheDefaultResponse()
    {
        var authorization = AuthorizationReturningTo(null);

        Assert.Equal("default", Outcome(authorization, new Describing(("sign_in", "pending"))));
    }

    /// <summary>
    /// A caller that did not opt in (one that creates a session on any redirect, such as
    /// Authorization.AuthenticateWithSessionAsync) must get the failure, even when the provider
    /// would redirect: a refusal must never reach a success continuation.
    /// </summary>
    [Fact]
    public void ACallerWithoutARejectionContinuation_AlwaysGetsTheFailure()
    {
        var authorization = AuthorizationReturningTo(new Uri("https://app.example.com/login"));

        var outcome = Redirection.Refused(authorization, Why,
                new Describing(("sign_in", "pending")), Request,
            onRejectionRedirect: null!,
            onGeneralFailure: why => $"failed: {why}");

        Assert.Equal($"failed: {Why}", outcome);
    }

    [Fact]
    public void ACallerWithARejectionContinuation_IsRedirected()
    {
        var authorization = AuthorizationReturningTo(new Uri("https://app.example.com/login"));

        var outcome = Redirection.Refused(authorization, Why,
                new Describing(("sign_in", "pending")), Request,
            onRejectionRedirect: uri => uri.AbsoluteUri,
            onGeneralFailure: why => $"failed: {why}");

        Assert.Contains("sign_in=pending", outcome);
    }

    [Fact]
    public void AReturnLocationWithoutAQuery_StillCarriesTheProvidersParameters()
    {
        var authorization = AuthorizationReturningTo(new Uri("https://app.example.com/login"));

        var outcome = Outcome(authorization, new Describing(("sign_in", "pending")));

        Assert.Equal("pending",
            System.Web.HttpUtility.ParseQueryString(new Uri(outcome).Query)["sign_in"]);
    }

    private class Plain : IProvideAccountInformation
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

    /// <summary>
    /// Redirects every rejection to the return location plus <c>parameters</c>, or declines when
    /// given none; records what it was handed.
    /// </summary>
    private sealed class Describing : Plain, IProvideRejectionRedirect
    {
        private readonly (string key, string value)[] parameters;

        public Describing(params (string key, string value)[] parameters) => this.parameters = parameters;

        public string? SeenWhy { get; private set; }
        public Uri? SeenReturnLocation { get; private set; }
        public IHttpRequest? SeenRequest { get; private set; }

        public TResult DescribeRejection<TResult>(string why, Uri returnLocation, IHttpRequest request,
            Func<Uri, TResult> onRedirect,
            Func<TResult> onKeepDefault)
        {
            SeenWhy = why;
            SeenReturnLocation = returnLocation;
            SeenRequest = request;
            if (parameters.Length == 0)
                return onKeepDefault();
            var uri = returnLocation;
            foreach (var (key, value) in parameters)
                uri = uri.SetQueryParam(key, value);
            return onRedirect(uri);
        }
    }
}
