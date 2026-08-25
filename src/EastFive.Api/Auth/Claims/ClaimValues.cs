using System;
namespace EastFive.Api.Auth
{
    public static class ClaimValues
    {
        public const string RoleType = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/role?id=";
        public const string DefaultAccountClaim = "sub";

        public static class Roles
        {
            public const string SuperAdmin = "superadmin";

            public const string PIIAdminRoldId = "b253931513424afb83d5bff92498548b";
            public const string PIIAdminRole = RoleType + PIIAdminRoldId;
            
            public const string SecurityReaderRoleId = "d8208df8d53341b9b647a802a91f56b6";
            public const string SecurityReaderRole = RoleType + SecurityReaderRoleId;
        }
    }
}

