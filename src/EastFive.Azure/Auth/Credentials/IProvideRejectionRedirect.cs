using System;

using EastFive.Api;

namespace EastFive.Azure.Auth
{
    /// <summary>
    /// Optional companion to <see cref="IProvideAccountInformation"/>. When the account provider
    /// refuses a sign-in (<c>onReject</c>), EastFive's default answer is the refusal text as bare
    /// HTML on the provider's redirect endpoint. A provider that also implements this interface
    /// can send the person back to the app they started from instead: it is handed the
    /// authorization's <see cref="Authorization.LocationAuthenticationReturn"/> (resolved to an
    /// absolute URI) and returns where to go, typically that location plus a reason code the app
    /// turns into its own message.
    ///
    /// The creator of the authorization chose that location, and EastFive does not validate it,
    /// so the provider should check it (for example that it is on the host the sign-in came back
    /// to) and keep the default when it does not trust it. The authorization stays as the refusal
    /// left it: unauthorized, and EastFive adds nothing to the redirect, so it never carries the
    /// authorization's id. Keep detail out of the URI; it lands in the address bar and history.
    /// </summary>
    public interface IProvideRejectionRedirect
    {
        /// <param name="why">The reason the provider passed to <c>onReject</c>.</param>
        /// <param name="returnLocation">The authorization's return location, absolute.</param>
        /// <param name="request">The provider's redirect request the refusal answers.</param>
        /// <param name="onRedirect">Redirect to this URI instead of the default response.</param>
        /// <param name="onKeepDefault">Keep EastFive's default response for this refusal.</param>
        TResult DescribeRejection<TResult>(string why, Uri returnLocation, IHttpRequest request,
            Func<Uri, TResult> onRedirect,
            Func<TResult> onKeepDefault);
    }
}
