using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Web;

using Xunit;

using EastFive.Azure.Auth.Google;

namespace EastFive.Azure.Tests.Auth;

/// <summary>
/// <see cref="GoogleProvider"/>: a sign-in asks for the person's name (<c>profile</c>), and the
/// name claims it brings back answer the standard claim types an account is populated from.
/// </summary>
public class GoogleProviderTests
{
    private static GoogleProvider Provider() =>
        new("client-id", "secret",
            new Uri("https://accounts.google.com/o/oauth2/v2/auth"),
            new Uri("https://oauth2.googleapis.com/token"),
            "https://accounts.google.com",
            keys: null!);

    [Fact]
    public void TheLoginUrl_RequestsOpenIdEmailAndProfile()
    {
        var url = Provider().GetLoginUrl(Guid.NewGuid(),
            new Uri("https://app.example.com/auth/GoogleRedirect"), _ => null!);

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
