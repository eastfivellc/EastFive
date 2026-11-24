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

    private static string Outcome(Authorization authorization, IProvideAccountInformation provider) =>
        Redirection.RejectionRedirect(authorization, Why, provider,
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

    /// <summary>Describes every rejection with <paramref name="parameters"/>, or declines when given none.</summary>
    private sealed class Describing : Plain, IProvideRejectionRedirect
    {
        private readonly (string key, string value)[] parameters;

        public Describing(params (string key, string value)[] parameters) => this.parameters = parameters;

        public TResult DescribeRejection<TResult>(string why,
            Func<KeyValuePair<string, string>[], TResult> onRedirect,
            Func<TResult> onKeepDefault) =>
            parameters.Length == 0
                ? onKeepDefault()
                : onRedirect(Array.ConvertAll(parameters, p => new KeyValuePair<string, string>(p.key, p.value)));
    }
}
