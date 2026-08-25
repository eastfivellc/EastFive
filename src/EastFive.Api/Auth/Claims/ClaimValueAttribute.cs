using System;

namespace EastFive.Api.Auth
{
    /// <summary>
    /// Declares an assignable claim (concrete type + value) on the application
    /// class (or assembly). Multiple may be applied. Surfaced by
    /// <c>GET /api/Claim/Assignable</c> so an admin UI can present the available
    /// claims as checkboxes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly,
        AllowMultiple = true, Inherited = true)]
    public class ClaimValueAttribute : Attribute, IDeclareClaimValue
    {
        public ClaimValueAttribute(string name, string type, string value, string description = "")
        {
            this.ClaimName = name;
            this.ClaimType = new Uri(type);
            this.ClaimValue = value;
            this.ClaimDescription = description;
        }

        public string ClaimName { get; }
        public Uri ClaimType { get; }
        public string ClaimValue { get; }
        public string ClaimDescription { get; }
    }
}
