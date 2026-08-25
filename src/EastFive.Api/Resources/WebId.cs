// using EastFive;
// using EastFive.Api;
// using EastFive.Extensions;
// using Newtonsoft.Json;
// using System;
// using System.Linq;
// using System.Runtime.Serialization;

// namespace EastFive.Api.Resources
// {
//     [DataContract(Name = "web_id")]
//     public class WebId
//     {
//         public WebId()
//         {
//         }

//         public WebId(string key, Guid uuid, Uri urn, Uri source)
//         {
//             this.Key = key;
//             this.UUID = uuid;
//             this.URN = urn;
//             this.Source = source;
//         }

//         /// <summary>
//         /// The key is a string that uniquely identifies this object among similar objects in a localized environment.
//         /// The key is not guarenteed to be globally unique but on modern distributed systems it is often a GUID.
//         /// </summary>
//         [DataMember(Name = "key")]
//         [JsonProperty(PropertyName = "key")]
//         public string Key { get; set; }

//         /// <summary>
//         /// The UUID is a global/universal unique identifier for this object. However, this does not contain
//         /// information about what type of object this is or where it is located.
//         /// </summary>
//         [DataMember(Name = "uuid")]
//         [JsonProperty(PropertyName = "uuid")]
//         public Guid UUID { get; set; }

//         /// <summary>
//         /// The URN identifier is globally unique and includes information about what type of
//         /// object is being identified but does not include information about where it is located.
//         /// </summary>
//         [DataMember(Name = "urn")]
//         [JsonProperty(PropertyName = "urn")]
//         public Uri URN { get; set; }

//         /// <summary>
//         /// The source identifier specifies the authoritative location where the object
//         /// can be accessed / updated.
//         /// </summary>
//         [DataMember(Name = "source")]
//         [JsonProperty(PropertyName = "source")]
//         public Uri Source { get; set; }

//         //public static implicit operator WebId(ResourceBase value)
//         //{
//         //    return (default(ResourceBase) == value) ? default(WebId) : value.Id;
//         //}
//     }
// }
