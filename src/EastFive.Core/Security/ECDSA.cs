using System;
using System.Security.Cryptography;
using EastFive.Extensions;
using EastFive.Web.Configuration;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;

namespace EastFive.Security;

public static class ECDSA
{
    // See this site for curve names:
    // https://www.iana.org/assignments/tls-parameters/tls-parameters.xhtml#tls-parameters-8
    public static TResult GenerateKey<TResult>(
    Func<string, // key
        TResult> onSuccess,
        string asn1CurveName = "secp384r1") // a.k.a. ES384
    {
        var curve = ECCurve.CreateFromFriendlyName(asn1CurveName);
        using (var provider = ECDsa.Create(curve))
        {
            var privateKey = provider.ExportECPrivateKey();
            return onSuccess(privateKey.ToBase64String());
        }
    }

    public static TResult FromPrivateKey<TResult>(byte[] privateKey,
        Func<System.Security.Cryptography.ECDsa, 
            string, // alg
            TResult> onSuccess,
        Func<string, TResult> onFailure)
    {
        try
        {
            var ecdsa = ECDsa.Create();
            ecdsa.ImportECPrivateKey(privateKey, out int bytesRead);
            var alg = $"ES{ecdsa.KeySize}";
            return onSuccess(ecdsa, alg);
        }
        catch (Exception ex)
        {
            return onFailure(ex.Message);
        }
    }

    public static TResult FromConfig<TResult>(string privateKeyConfigurationName,
        Func<System.Security.Cryptography.ECDsa, 
            string, // alg
            TResult> onSuccess,
        Func<TResult> onNotSpecified,
        Func<string, TResult> onFailure)
    {
        return privateKeyConfigurationName.ConfigurationBase64Bytes(
            (keyBytes) => FromPrivateKey(keyBytes, onSuccess, onFailure),               
            onFailure,
            onNotSpecified);
    }

    public static TResult GetPublicJWK<TResult>(this System.Security.Cryptography.ECDsa ecdsa,
        Func<string,
            TResult> onSuccess,
            string kid = default,
            string use = "sig")
    {
        const string kty = "EC";

        if (kid.IsNullOrWhiteSpace())
        {
            var key = new ECDsaSecurityKey(ecdsa);
            if (key.CanComputeJwkThumbprint())
                kid = key.ComputeJwkThumbprint().ToBase64String();
            else
                kid = DateTime.Now.ToString("O");
        }

        var ecparams = ecdsa.ExportParameters(true);
        var crv = $"P-{ecdsa.KeySize}";
        var x = ecparams.Q.X.ToBase64String();
        var y = ecparams.Q.Y.ToBase64String();
        var jwk = JsonConvert.SerializeObject(
            new {
                kid,
                crv,
                kty,
                use,
                x,
                y,
            },
            Formatting.Indented
        );
        return onSuccess(jwk);
    }
}
