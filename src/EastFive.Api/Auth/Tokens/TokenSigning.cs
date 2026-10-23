#nullable enable
using System;
using System.Security.Cryptography;

using EastFive.Configuration;

namespace EastFive.Security.Tokens
{
    /// <summary>
    /// What signs and validates the tokens EastFive issues: the issuer, the RSA key (base64 of the
    /// RSA XML, private for signing) and the algorithm. Handed to <see cref="JwtTools"/> explicitly,
    /// or resolved from <see cref="TokenSigningConfiguration"/> as the host loaded it at startup.
    /// </summary>
    public sealed record TokenSigning(string Issuer, Secret RsaKey, string Algorithm)
    {
        public const string DefaultAlgorithm = Microsoft.IdentityModel.Tokens.SecurityAlgorithms.RsaSha256Signature;

        /// <summary>The host's signing configuration (<see cref="StartupConfiguration"/>); never loads.</summary>
        public static TResult Current<TResult>(Func<TokenSigning, TResult> onLoaded, Func<ConfigIssue, TResult> onUnavailable)
            => StartupConfiguration.Get<TokenSigningConfiguration, TResult>(
                config => onLoaded(config.ToSigning()), onUnavailable);

        public TResult WithRsa<TResult>(Func<RSACryptoServiceProvider, TResult> onKey, Func<string, TResult> onInvalidKey)
            => RsaKey.RSAFromSecret(onKey, onInvalidKey);
    }

    /// <summary>The token issuer and signing key: <c>EastFive.Security.Token.Issuer</c> /
    /// <c>.Algorithm</c> from the values store, the key from the secret store as
    /// <c>EastFiveTokens-SigningKey</c>.</summary>
    [ConfigurationType("EastFiveTokens", Label = "Token signing",
        Description = "Issuer and RSA key EastFive signs and validates its access tokens with.")]
    public sealed class TokenSigningConfiguration : IProvideConfiguration
    {
        [ConfigurationMember(EastFive.Security.AppSettings.TokenIssuer, Label = "Issuer",
            Description = "The iss of every token this host issues and accepts.")]
        public string Issuer { get; set; } = default!;

        [ConfigurationMember(EastFive.Security.AppSettings.TokenAlgorithm, Kind = ConfigurationMemberKind.Defaulted,
            Default = TokenSigning.DefaultAlgorithm, Scope = ConfigurationScope.Shared)]
        public string Algorithm { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "Signing key",
            Description = "Base64 of the RSA key XML (private).", Location = "Generated: EastFive.Security.RSA.Generate")]
        public Secret SigningKey { get; set; } = default!;

        [ConfigurationMember(EastFive.Security.AppSettings.TokenScope, Kind = ConfigurationMemberKind.Optional,
            Label = "Scope", Description = "The audience (aud) of tokens this host issues.")]
        public Uri? Scope { get; set; }

        [ConfigurationMember(EastFive.Security.AppSettings.TokensAllValid, Kind = ConfigurationMemberKind.Defaulted, Default = "false",
            Label = "Accept every token", Description = "Read claims WITHOUT validating signature or lifetime. Never in a deployed environment.")]
        public bool AllValid { get; set; }

        [ConfigurationMember(EastFive.Web.AppSettings.TokenForceRefreshTime, Kind = ConfigurationMemberKind.Optional,
            Label = "Refuse tokens issued before", Description = "Tokens issued before this time are refused, forcing clients to sign in again.",
            Location = "Typically the date/time of a web client release")]
        public DateTime? ForceRefreshBefore { get; set; }

        [ConfigurationMember(EastFive.Web.AppSettings.TokenForceRefreshMessage, Kind = ConfigurationMemberKind.Optional,
            Label = "Refused-token message", Description = "The reason given for a token refused by the force-refresh time.")]
        public string? ForceRefreshMessage { get; set; }

        /// <summary>The token scope, in the raw reader's continuation shape.</summary>
        public static TResult WithScope<TResult>(Func<Uri, TResult> onFound,
            Func<string, TResult>? onUnspecified = null, Func<TResult>? onNotSpecified = null)
            => Configured.Member<TokenSigningConfiguration, Uri, TResult>(config => config.Scope,
                EastFive.Security.AppSettings.TokenScope, onFound, onUnspecified, onNotSpecified);

        /// <summary>The token issuer, in the raw reader's continuation shape.</summary>
        public static TResult WithIssuer<TResult>(Func<string, TResult> onFound,
            Func<string, TResult>? onUnspecified = null, Func<TResult>? onNotSpecified = null)
            => Configured.Member<TokenSigningConfiguration, string, TResult>(config => config.Issuer,
                EastFive.Security.AppSettings.TokenIssuer, onFound, onUnspecified, onNotSpecified);

        /// <summary>The scope, or a <see cref="EastFive.Web.ConfigurationException"/> naming its key.</summary>
        public static TResult CurrentScope<TResult>(Func<Uri, TResult> onScope, Func<string, TResult> onUnavailable)
            => StartupConfiguration.Get<TokenSigningConfiguration, TResult>(
                config => config.Scope is { } scope ? onScope(scope) : onUnavailable($"[{EastFive.Security.AppSettings.TokenScope}] is not configured."),
                issue => onUnavailable(issue.ToString()));

        public string? Validate()
            => SigningKey == null || SigningKey.RSAFromSecret(rsa => { rsa.Dispose(); return true; }, _ => false)
                ? null
                : ((IProvideConfiguration)this).SecretReference(nameof(SigningKey)).Name;

        public TokenSigning ToSigning() => new(Issuer, SigningKey, string.IsNullOrWhiteSpace(Algorithm) ? TokenSigning.DefaultAlgorithm : Algorithm);
    }
}
