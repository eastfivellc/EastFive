using System;
using System.Collections.Generic;

namespace EastFive.Azure.Auth
{
    /// <summary>
    /// Optional companion to <see cref="IProvideAccountInformation"/>. When the account provider
    /// refuses a sign-in (<c>onReject</c>), EastFive's default answer is the refusal text as bare
    /// HTML on the provider's redirect endpoint. A provider that also implements this interface
    /// can send the person back to the app they started from instead: the authorization's
    /// <see cref="Authorization.LocationAuthenticationReturn"/>, with query parameters the
    /// provider chooses (for example a reason code the app turns into its own message).
    ///
    /// The authorization stays as the refusal left it: unauthorized, and the redirect never
    /// carries its id. Keep detail out of the parameters; they land in the browser's address
    /// bar and history.
    /// </summary>
    public interface IProvideRejectionRedirect
    {
        /// <param name="why">The reason the provider passed to <c>onReject</c>.</param>
        /// <param name="onRedirect">Redirect to the return location with these query parameters.</param>
        /// <param name="onKeepDefault">Keep EastFive's default response for this refusal.</param>
        TResult DescribeRejection<TResult>(string why,
            Func<KeyValuePair<string, string>[], TResult> onRedirect,
            Func<TResult> onKeepDefault);
    }
}
