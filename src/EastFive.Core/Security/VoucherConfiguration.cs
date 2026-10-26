#nullable enable
using System;
using System.Security.Cryptography;

using EastFive.Configuration;

namespace EastFive.Security
{
    /// <summary>The RSA key that signs and verifies voucher tokens. Secret <c>EastFiveVoucher-SigningKey</c>
    /// (base64 of the RSA key XML), loaded by the host at startup.</summary>
    [ConfigurationType("EastFiveVoucher", Label = "Voucher credentials", Description = "The key that signs voucher tokens, and the provider that issues them.", Scope = ConfigurationScope.Environment)]
    public sealed class VoucherConfiguration : IProvideConfiguration
    {
        public const string ProviderIdKey = "EastFive.Security.CredentialProvider.Voucher.Provider";

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Description = "Base64 of the RSA key XML.", Label = "Signing key")]
        public Secret SigningKey { get; set; } = default!;

        [ConfigurationMember(ProviderIdKey, Kind = ConfigurationMemberKind.Optional,
            Label = "Trusted provider", Description = "Provider id vouchers are issued under.")]
        public Uri? ProviderId { get; set; }

        /// <summary>The host's voucher key, opened. Never loads.</summary>
        public static TResult WithKey<TResult>(Func<RSACryptoServiceProvider, TResult> onKey,
            Func<TResult> onMissing, Func<string, TResult> onInvalid)
            => StartupConfiguration.Get<VoucherConfiguration, TResult>(
                configuration => configuration.SigningKey.RSAFromSecret(onKey, onInvalid),
                issue => issue.Category == ConfigIssueCategory.Invalid ? onInvalid(issue.ToString()) : onMissing());
    }
}
