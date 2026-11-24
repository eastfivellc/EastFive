using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Web;

using Xunit;

using EastFive.Azure.Auth.Microsoft;

namespace EastFive.Azure.Tests.Auth;

/// <summary>
/// <see cref="MicrosoftProvider"/>: a sign-in asks for the person's name (<c>profile</c>), and the
/// name claims it brings back answer the standard claim types an account is populated from.
/// </summary>
public class MicrosoftProviderTests
{
    private static MicrosoftProvider Provider() =>
        new("client-id", "secret",
            new Uri("https://login.microsoftonline.com/common/oauth2/v2.0/authorize"),
            new Uri("https://login.microsoftonline.com/common/oauth2/v2.0/token"),
            "https://login.microsoftonline.com/{tenantid}/v2.0",
            keys: null!);

    [Fact]
    public void TheLoginUrl_RequestsOpenIdEmailAndProfile()
    {
        var url = Provider().GetLoginUrl(Guid.NewGuid(),
            new Uri("https://app.example.com/auth/MicrosoftRedirect"), _ => null!);

        var scopes = HttpUtility.ParseQueryString(url.Query)["scope"]!.Split(' ');
        Assert.Contains("openid", scopes);
        Assert.Contains("email", scopes);
        Assert.Contains("profile", scopes);
    }

    [Theory]
    [InlineData(ClaimTypes.GivenName, "given_name", "Colleen")]
    [InlineData(ClaimTypes.Surname, "family_name", "Tobe-Donohue")]
    [InlineData(ClaimTypes.Name, "name", "Colleen Tobe-Donohue")]
    public void NameClaims_AnswerTheStandardClaimTypes(string claimType, string parameter, string value)
    {
        var found = Provider().TryGetStandardClaimValue(claimType,
            new Dictionary<string, string> { [parameter] = value }, out var claimValue);

        Assert.True(found);
        Assert.Equal(value, claimValue);
    }
}
