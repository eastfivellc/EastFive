using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.AzureADB2C.Resources
{
    [DataContract]
    public class User
    {
        [JsonProperty("objectId")]
        public string ObjectId { get; set; }

        [JsonProperty("accountEnabled")]
        public bool AccountEnabled { get; set; }

        [JsonProperty("creationType")]
        public string CreationType
        {
            get
            {
                return "LocalAccount";
            }
            set { }
        }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("passwordProfile")]
        public PasswordProfileResource PasswordProfile { get; set; }

        public class PasswordProfileResource
        {

            [JsonProperty("password")]
            public string Password { get; set; }

            [JsonProperty("forceChangePasswordNextLogin")]
            public bool ForceChangePasswordNextLogin { get; set; }
        }

        [JsonProperty("signInNames")]
        public SignInName[] SignInNames { get; set; }

        public class SignInName
        {
            /// <summary>
            /// Values: emailAddress | userName
            /// </summary>
            [JsonProperty("type")]
            public string Type { get; set; }

            [JsonProperty("value")]
            public string Value { get; set; }
        }

        [JsonProperty("otherMails")]
        public string[] OtherMails { get; set; }
    }

    [DataContract]
    public class UserPasswordPatch
    {
        [JsonProperty("objectId")]
        public string ObjectId { get; set; }
        
        [JsonProperty("passwordProfile")]
        public User.PasswordProfileResource PasswordProfile { get; set; }
        
    }

    [DataContract]
    public class UserEmailPatch
    {
        [JsonProperty("objectId")]
        public string ObjectId { get; set; }

        [JsonProperty("otherMails")]
        public string[] OtherMails { get; set; }
    }
}