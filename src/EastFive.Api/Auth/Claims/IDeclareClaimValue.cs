using System;

namespace EastFive.Api.Auth
{
    /// <summary>
    /// Declares a concrete, assignable claim (a specific claim type + value the
    /// system recognizes — e.g. a role grant). Discovered by the Claim
    /// controller's "Assignable" endpoint so an admin UI can present the
    /// available claims as checkboxes. Contrast with <see cref="IDeclareClaim"/>,
    /// which declares claim *types* (no concrete value).
    /// </summary>
    public interface IDeclareClaimValue
    {
        string ClaimName { get; }
        Uri ClaimType { get; }
        string ClaimValue { get; }
        string ClaimDescription { get; }
    }
}
