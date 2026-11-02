#nullable enable
using System;
using System.Security.Cryptography;

using EastFive.Configuration;

namespace EastFive.Security.Tokens
{
    /// <summary>
    /// What signs and validates the tokens EastFive issues: the issuer, the RSA private key and the
    /// algorithm. Handed to <see cref="JwtTools"/> explicitly, or resolved from
    /// <see cref="TokenSigningConfiguration"/> as the host loaded it at startup.
    /// </summary>
    public sealed record TokenSigning(string Issuer, RsaPrivateKey Key, string Algorithm)
    {
        public const string DefaultAlgorithm = Microsoft.IdentityModel.Tokens.SecurityAlgorithms.RsaSha256Signature;

        /// <summary>The host's signing configuration (<see cref="StartupConfiguration"/>); never loads.</summary>
        public static TResult Current<TResult>(Func<TokenSigning, TResult> onLoaded, Func<ConfigIssue, TResult> onUnavailable)
            => StartupConfiguration.Get<TokenSigningConfiguration, TResult>(
                config => onLoaded(config.ToSigning()), onUnavailable);

        /// <summary>The key's shared RSA instance; the callee must not dispose it.</summary>
        public TResult WithRsa<TResult>(Func<System.Security.Cryptography.RSA, TResult> onKey) => onKey(Key.Rsa);

        /// <summary>The JWS name (<c>alg</c>) of <see cref="Algorithm"/>, which may be configured as an
        /// XML-DSig URI; the key's own algorithm when the configured one is not an RSA JWS algorithm.</summary>
        public string JwsAlgorithm => Algorithm switch
        {
            Microsoft.IdentityModel.Tokens.SecurityAlgorithms.RsaSha256Signature => "RS256",
            Microsoft.IdentityModel.Tokens.SecurityAlgorithms.RsaSha384Signature => "RS384",
            Microsoft.IdentityModel.Tokens.SecurityAlgorithms.RsaSha512Signature => "RS512",
            Microsoft.IdentityModel.Tokens.SecurityAlgorithms.RsaSsaPssSha256Signature => "PS256",
            Microsoft.IdentityModel.Tokens.SecurityAlgorithms.RsaSsaPssSha384Signature => "PS384",
            Microsoft.IdentityModel.Tokens.SecurityAlgorithms.RsaSsaPssSha512Signature => "PS512",
            var jws when RsaPrivateKey.IsAlgorithm(jws) => jws,
            _ => Key.Algorithm,
        };
    }

    /// <summary>The token issuer and signing key: <c>EastFive.Security.Token.Issuer</c> /
    /// <c>.Algorithm</c> from the values store, the key from the secret store as
    /// <c>EastFiveTokens-SigningKey</c>.</summary>
    [ConfigurationType("EastFiveTokens", Label = "Token signing",
        Description = "Issuer and RSA key EastFive signs and validates its access tokens with.", Scope = ConfigurationScope.Environment)]
    public sealed class TokenSigningConfiguration : IProvideConfiguration
    {
        public const string AlgorithmKey = "EastFive.Security.Token.Algorithm";
        public const string AllValidKey = "EastFive.Security.Token.AllValid";
        public const string ForceRefreshBeforeKey = "EastFive.Web.TokenForceRefreshTime";
        public const string ForceRefreshMessageKey = "EastFive.Web.TokenForceRefreshMessage";
        public const string IssuerKey = "EastFive.Security.Token.Issuer";
        public const string ScopeKey = "EastFive.Security.Token.Scope";

        [ConfigurationMember(IssuerKey, Label = "Issuer",
            Description = "The iss of every token this host issues and accepts.")]
        public string Issuer { get; set; } = default!;

        [ConfigurationMember(AlgorithmKey, Kind = ConfigurationMemberKind.Defaulted,
            Default = TokenSigning.DefaultAlgorithm, Scope = ConfigurationScope.Shared, Label = "Algorithm", Description = "JWT signing algorithm.")]
        public string Algorithm { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "Signing key",
            Description = "The RSA private key tokens are signed with.", Location = "Generate it in the configuration editor")]
        [RsaKey(Instruction = "Tokens signed with the previous key stop validating once the new key is saved and the host restarts; signed-in users sign in again.")]
        public RsaPrivateKey SigningKey { get; set; } = default!;

        [ConfigurationMember(ScopeKey, Kind = ConfigurationMemberKind.Optional,
            Label = "Scope", Description = "The audience (aud) of tokens this host issues.")]
        public Uri? Scope { get; set; }

        [ConfigurationMember(AllValidKey, Kind = ConfigurationMemberKind.Defaulted, Default = "false",
            Label = "Accept every token", Description = "Read claims WITHOUT validating signature or lifetime. Never in a deployed environment.")]
        public bool AllValid { get; set; }

        [ConfigurationMember(ForceRefreshBeforeKey, Kind = ConfigurationMemberKind.Optional,
            Label = "Refuse tokens issued before", Description = "Tokens issued before this time are refused, forcing clients to sign in again.",
            Location = "Typically the date/time of a web client release")]
        public DateTime? ForceRefreshBefore { get; set; }

        [ConfigurationMember(ForceRefreshMessageKey, Kind = ConfigurationMemberKind.Optional,
            Label = "Refused-token message", Description = "The reason given for a token refused by the force-refresh time.")]
        public string? ForceRefreshMessage { get; set; }

        /// <summary>The token scope, in the raw reader's continuation shape.</summary>
        public static TResult WithScope<TResult>(Func<Uri, TResult> onFound,
            Func<string, TResult>? onUnspecified = null, Func<TResult>? onNotSpecified = null)
            => Configured.Member<TokenSigningConfiguration, Uri, TResult>(config => config.Scope,
                ScopeKey, onFound, onUnspecified, onNotSpecified);

        /// <summary>The token issuer, in the raw reader's continuation shape.</summary>
        public static TResult WithIssuer<TResult>(Func<string, TResult> onFound,
            Func<string, TResult>? onUnspecified = null, Func<TResult>? onNotSpecified = null)
            => Configured.Member<TokenSigningConfiguration, string, TResult>(config => config.Issuer,
                IssuerKey, onFound, onUnspecified, onNotSpecified);

        /// <summary>The scope, or a <see cref="EastFive.Web.ConfigurationException"/> naming its key.</summary>
        public static TResult CurrentScope<TResult>(Func<Uri, TResult> onScope, Func<string, TResult> onUnavailable)
            => StartupConfiguration.Get<TokenSigningConfiguration, TResult>(
                config => config.Scope is { } scope ? onScope(scope) : onUnavailable($"[{ScopeKey}] is not configured."),
                issue => onUnavailable(issue.ToString()));

        public TokenSigning ToSigning() => new(Issuer, SigningKey, string.IsNullOrWhiteSpace(Algorithm) ? TokenSigning.DefaultAlgorithm : Algorithm);
    }
}
