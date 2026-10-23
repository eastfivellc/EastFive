#nullable enable
using System;
using System.Security.Cryptography;

using EastFive.Configuration;

namespace EastFive.Security
{
    /// <summary>The RSA key that signs and verifies voucher tokens. Secret <c>EastFiveVoucher-SigningKey</c>
    /// (base64 of the RSA key XML), loaded by the host at startup.</summary>
    [ConfigurationType("EastFiveVoucher", Label = "Voucher credentials")]
    public sealed class VoucherConfiguration : IProvideConfiguration
    {
        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Description = "Base64 of the RSA key XML.")]
        public Secret SigningKey { get; set; } = default!;

        /// <summary>The host's voucher key, opened. Never loads.</summary>
        public static TResult WithKey<TResult>(Func<RSACryptoServiceProvider, TResult> onKey,
            Func<TResult> onMissing, Func<string, TResult> onInvalid)
            => StartupConfiguration.Get<VoucherConfiguration, TResult>(
                configuration => configuration.SigningKey.RSAFromSecret(onKey, onInvalid),
                issue => issue.Category == ConfigIssueCategory.Invalid ? onInvalid(issue.ToString()) : onMissing());
    }
}
