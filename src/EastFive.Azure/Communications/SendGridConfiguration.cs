#nullable enable
using System;

using EastFive.Configuration;

namespace EastFive.Azure.Communications
{
    /// <summary>How SendGrid mail is redirected and copied outside production. Values only: the API key
    /// is handed to <see cref="EastFive.Communications.Azure.SendGridMailer"/> by its host.</summary>
    [ConfigurationType("SendGrid", Label = "SendGrid mail",
        Description = "Redirect (mute) or blind-copy every SendGrid email, for non-production environments.")]
    public sealed class SendGridConfiguration : IProvideConfiguration
    {
        [ConfigurationMember(AppSettings.Communications.MuteEmailToAddress, Kind = ConfigurationMemberKind.Optional,
            Label = "Mute to address", Description = "When set, every email goes to this address instead of its recipient.")]
        public string? MuteEmailToAddress { get; set; }

        [ConfigurationMember(AppSettings.Communications.BccAllAddresses, Kind = ConfigurationMemberKind.Optional,
            Label = "Blind-copy addresses", Description = "Comma-separated addresses blind-copied on every email.")]
        public string? BccAllAddresses { get; set; }

        public static SendGridConfiguration? Current()
            => Configured.Get<SendGridConfiguration, SendGridConfiguration?>(config => config, _ => null);
    }
}
