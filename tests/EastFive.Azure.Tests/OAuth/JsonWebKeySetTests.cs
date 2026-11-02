using System;
using System.Security.Cryptography;
using System.Text;

using Xunit;

using EastFive.Azure.OAuth.Server;
using EastFive.Security;
using EastFive.Security.Tokens;

namespace EastFive.Azure.Tests.OAuth;

/// <summary>
/// What <c>/.well-known/jwks.json</c> publishes for the configured signing key: the key id keeps its
/// existing derivation (vendors registered it), and <c>alg</c> follows the configured algorithm
/// rather than a hard-coded RS256.
/// </summary>
public class JsonWebKeySetTests
{
    private static RsaPrivateKey TodaysDevFormatKey(System.Security.Cryptography.RSA rsa)
        => RsaPrivateKey.Parse(Convert.ToBase64String(Encoding.ASCII.GetBytes(rsa.ToXmlString(true))),
            key => key, why => throw new InvalidOperationException(why));

    [Theory]
    [InlineData(Microsoft.IdentityModel.Tokens.SecurityAlgorithms.RsaSha256Signature, "RS256")]
    [InlineData(Microsoft.IdentityModel.Tokens.SecurityAlgorithms.RsaSha512Signature, "RS512")]
    [InlineData("PS384", "PS384")]
    [InlineData("not-an-algorithm", "RS256")]
    public void The_published_alg_follows_the_configured_algorithm(string configured, string published)
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var signing = new TokenSigning("https://issuer.example/", TodaysDevFormatKey(rsa), configured);
        Assert.Equal(published, signing.JwsAlgorithm);
        var jwk = signing.WithRsa(opened => JwkKey.FromRsaParameters(opened.ExportParameters(false), signing.JwsAlgorithm));
        Assert.Equal(published, jwk.Algorithm);
    }

    [Fact]
    public void The_key_id_and_public_members_are_unchanged_for_todays_key()
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var parameters = rsa.ExportParameters(false);
        var signing = new TokenSigning("https://issuer.example/", TodaysDevFormatKey(rsa), TokenSigning.DefaultAlgorithm);

        var jwk = signing.WithRsa(opened => JwkKey.FromRsaParameters(opened.ExportParameters(false), signing.JwsAlgorithm));

        Assert.Equal(JwkKey.ComputeKeyId(parameters), jwk.KeyId);
        Assert.Equal(OAuthServer.Base64UrlEncode(parameters.Modulus), jwk.Modulus);
        Assert.Equal(OAuthServer.Base64UrlEncode(parameters.Exponent), jwk.Exponent);
        Assert.Equal("RSA", jwk.KeyType);
        Assert.Equal("sig", jwk.Use);
    }
}
