using System;
using System.Linq;
using System.Security.Claims;
using EastFive.Linq;

namespace EastFive.Api.Auth
{
    public struct Token
    {
        public const string PropertyName = "x-eastfive.api.auth.token";

        public Claim[] claims;

        public IProvideToken provider;

        public object source;

        public bool IsAuthorizedForRoll(string claimValue,
            StringComparison stringComparison = StringComparison.Ordinal)
        {
            return claims.First(
                (claim, next) =>
                {
                    if (!ClaimTypes.Role.Equals(claim.Type, stringComparison))
                        return next();

                    if (!claimValue.Equals(claim.Value, stringComparison))
                        return next();

                    return true;
                },
                () => false);
        }

        public bool IsAuthorizedFor(string claimType, string claimValue,
            StringComparison stringComparison = StringComparison.Ordinal)
        {
            return claims.First(
                (claim, next) =>
                {
                    if (!claimType.Equals(claim.Type, stringComparison))
                        return next();

                    if(!claimValue.Equals(claim.Value, stringComparison))
                        return next();

                    return true;
                },
                () => false);
        }

    }
}

