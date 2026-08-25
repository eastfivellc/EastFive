// using System;
// using System.Collections.Generic;
// using System.Text;

// using Newtonsoft.Json;

// using EastFive.Api.Meta.OpenApi;

// namespace EastFive.Api.Meta.Postman
// {
//     [FunctionViewController(Route = "PostmanLink")]
//     [OpenApiRoute(Collection = "EastFive.Api.Meta")]
//     public class PostmanLink : IReferenceable
//     {
//         #region Properties

//         [JsonIgnore]
//         public Guid id => this.postmanLinkRef.id;

//         public const string IdPropertyName = "id";
//         [JsonProperty(PropertyName = IdPropertyName)]
//         [ApiProperty(PropertyName = IdPropertyName)]
//         public IRef<PostmanLink> postmanLinkRef;

//         public const string AppIdPropertyName = "app_id";
//         [JsonProperty(PropertyName = AppIdPropertyName)]
//         [ApiProperty(PropertyName = AppIdPropertyName)]
//         public string appId;

//         public const string ApiKeyPropertyName = "api_key";
//         [JsonProperty(PropertyName = ApiKeyPropertyName)]
//         [ApiProperty(PropertyName = ApiKeyPropertyName)]
//         public string apiKey;

//         #endregion

//         [EastFive.Api.HttpPost]
//         public static IHttpResponse FindAsync(
//                 //Security security,
//                 HttpApplication application, IHttpRequest request, IProvideUrl url,
//             NoContentResponse onSuccess,
//             ViewFileResponse<Api.Resources.Manifest> onHtml)
//         {
//             return onSuccess();
//         }
//     }
// }
