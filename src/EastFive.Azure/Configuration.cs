using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Security.Claims;

using Microsoft.AspNetCore.Mvc.Routing;

using EastFive.Api;
using EastFive.Api.Auth;
using EastFive.Api.Controllers;
using EastFive.Azure.Auth;
using EastFive.Azure.Persistence.AzureStorageTables;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Linq.Async;
using EastFive.Linq.Expressions;
using EastFive.Persistence;
using EastFive.Persistence.Azure.StorageTables;
using EastFive.Web;
using EastFive.Web.Configuration;
using EastFive.Configuration;
using Newtonsoft.Json;
using EastFive.Reflection;
using EastFive.Azure.Meta;

namespace EastFive.Azure.Configuration
{
    [DataContract]
    [FunctionViewController(
        Route = "Configuration",
        ContentType = "x-application/auth-configuration",
        ContentTypeVersion = "0.1")]
    public struct Configuration
    {
        #region Properties

        public const string ConfigurationTypePropertyName = "configuration_type";
        /// <summary>The typed configuration (its generated-name segment, or the CLR name).</summary>
        [ApiProperty(PropertyName = ConfigurationTypePropertyName)]
        [JsonProperty(PropertyName = ConfigurationTypePropertyName)]
        public string configurationType;

        public const string NamePropertyName = "name";
        /// <summary>The member's property name.</summary>
        [ApiProperty(PropertyName = NamePropertyName)]
        [JsonProperty(PropertyName = NamePropertyName)]
        public string name;

        public const string KeyPropertyName = "key";
        /// <summary>The values key a value member is read from; null for a secret.</summary>
        [ApiProperty(PropertyName = KeyPropertyName)]
        [JsonProperty(PropertyName = KeyPropertyName)]
        public string key;

        public const string SecretNamePropertyName = "secret_name";
        /// <summary>The generated secret name (with an <c>{account}</c> placeholder) a secret is read by.</summary>
        [ApiProperty(PropertyName = SecretNamePropertyName)]
        [JsonProperty(PropertyName = SecretNamePropertyName)]
        public string secretName;

        public const string KindPropertyName = "kind";
        [ApiProperty(PropertyName = KindPropertyName)]
        [JsonProperty(PropertyName = KindPropertyName)]
        public string kind;

        public const string LabelPropertyName = "label";
        [ApiProperty(PropertyName = LabelPropertyName)]
        [JsonProperty(PropertyName = LabelPropertyName)]
        public string label;

        public const string DescriptionPropertyName = "description";
        /// <summary>What is provided by this value.</summary>
        [ApiProperty(PropertyName = DescriptionPropertyName)]
        [JsonProperty(PropertyName = DescriptionPropertyName)]
        public string description;

        public const string LocationPropertyName = "location";
        /// <summary>Where to find this value.</summary>
        [ApiProperty(PropertyName = LocationPropertyName)]
        [JsonProperty(PropertyName = LocationPropertyName)]
        public string location;

        public const string MoreInfoPropertyName = "more_info";
        [ApiProperty(PropertyName = MoreInfoPropertyName)]
        [JsonProperty(PropertyName = MoreInfoPropertyName)]
        public string moreInfo;

        public const string ScopePropertyName = "scope";
        /// <summary>Per-environment or shared.</summary>
        [ApiProperty(PropertyName = ScopePropertyName)]
        [JsonProperty(PropertyName = ScopePropertyName)]
        public string scope;

        public const string FieldKindPropertyName = "field_kind";
        [ApiProperty(PropertyName = FieldKindPropertyName)]
        [JsonProperty(PropertyName = FieldKindPropertyName)]
        public string fieldKind;

        public const string ValuePropertyName = "value";
        /// <summary>The current VALUE of a value member; never populated for a secret.</summary>
        [ApiProperty(PropertyName = ValuePropertyName)]
        [JsonProperty(PropertyName = ValuePropertyName)]
        public string value;

        #endregion

        /// <summary>
        /// Every typed configuration member loaded in this process, rendered from
        /// <see cref="ConfigurationCatalog"/>. Secrets are listed by generated name only; their
        /// values never leave the secret store.
        /// </summary>
        [ApiKeyClaim(ClaimTypes.Role, ClaimValues.Roles.SuperAdmin)]
        public static IHttpResponse GetAsync(
            ContentTypeResponse<Configuration[]> onFound)
        {
            var configs = ConfigurationCatalog.For(AppDomain.CurrentDomain.GetAssemblies())
                .SelectMany(type => type.Members.Select(member => new Configuration()
                {
                    configurationType = type.TypeName ?? type.Type.Name,
                    name = member.Property,
                    key = member.Key,
                    secretName = member.SecretNameTemplate,
                    kind = member.Kind.ToString(),
                    label = member.Metadata.Label,
                    description = member.Metadata.Description,
                    location = member.Metadata.Location,
                    moreInfo = member.Metadata.MoreInfo,
                    scope = member.Metadata.Scope.ToString(),
                    fieldKind = member.Metadata.FieldKind.ToString(),
                    value = ConfigurationCatalog.CurrentValue(member),
                }))
                .ToArray();
            return onFound(configs);
        }
    }

}