using System;
using System.Reflection;
using System.Threading.Tasks;
using EastFive.Reflection;
using Newtonsoft.Json;

namespace EastFive.Api
{
    [Link]
    public delegate System.Linq.IQueryable<TResource> Link<TResource>(System.Linq.IQueryable<TResource> api, TResource resource);

    public class LinkAttribute : System.Attribute, ICastJsonProperty
    {
        public bool CanConvert(MemberInfo member, ParameterInfo paramInfo, IHttpRequest httpRequest, IApplication application, IProvideApiValue apiValueProvider, object objectValue)
        {
            var type = member.GetPropertyOrFieldType();
            var isLink = type.IsSubClassOfGeneric(typeof(Link<>));
            return isLink;
        }

        public Task WriteAsync(JsonWriter writer, Newtonsoft.Json.JsonSerializer serializer, MemberInfo member,
            ParameterInfo paramInfo, IProvideApiValue apiValueProvider,
            object objectValue, object memberValue,
            IHttpRequest httpRequest, IApplication application)
        {
            var linkType = member.GetPropertyOrFieldType();
            Task result = (Task)typeof(LinkAttribute)
                .GetMethod(nameof(WriteGenericAsync), BindingFlags.Static | BindingFlags.Public)
                .MakeGenericMethod(linkType.GenericTypeArguments)
                .Invoke(null, new object[] { writer,
                    objectValue, memberValue, httpRequest});
            return result;
        }

        public static Task WriteGenericAsync<TResource>(JsonWriter writer,
            object objectValue, object memberValue,
            IHttpRequest httpRequest)
        {
            var urlBuilder = new UrlBuilder(httpRequest);
            var api = urlBuilder.Resources<TResource>();
            var link = memberValue as Link<TResource>;
            var resource = (TResource)objectValue;
            var query = link(api, resource);
            var url = query.Location();
            var urlString = url.OriginalString;
            return writer.WriteValueAsync(urlString);
        }
    }

    [LinkFromRequest]
    public delegate System.Linq.IQueryable<TResource> LinkFromRequest<TResource>(System.Linq.IQueryable<TResource> api,
        TResource resource, IHttpRequest request);

    public class LinkFromRequestAttribute : System.Attribute, ICastJsonProperty
    {
        public bool CanConvert(MemberInfo member, ParameterInfo paramInfo, IHttpRequest httpRequest, IApplication application, IProvideApiValue apiValueProvider, object objectValue)
        {
            var type = member.GetPropertyOrFieldType();
            var isLink = type.IsSubClassOfGeneric(typeof(LinkFromRequest<>));
            return isLink;
        }

        public Task WriteAsync(JsonWriter writer, JsonSerializer serializer, MemberInfo member,
            ParameterInfo paramInfo, IProvideApiValue apiValueProvider,
            object objectValue, object memberValue,
            IHttpRequest httpRequest, IApplication application)
        {
            var linkType = member.GetPropertyOrFieldType();
            Task result = (Task)typeof(LinkAttribute)
                .GetMethod(nameof(WriteGenericAsync), BindingFlags.Static | BindingFlags.Public)
                .MakeGenericMethod(linkType.GenericTypeArguments)
                .Invoke(null, new object[] { writer,
                    objectValue, memberValue, httpRequest});
            return result;
        }

        public static Task WriteGenericAsync<TResource>(JsonWriter writer,
            object objectValue, object memberValue,
            IHttpRequest httpRequest)
        {
            var urlBuilder = new UrlBuilder(httpRequest);
            var api = urlBuilder.Resources<TResource>();
            var link = memberValue as LinkFromRequest<TResource>;
            var resource = (TResource)objectValue;
            var query = link(api, resource, httpRequest);
            var url = query.Location();
            var urlString = url.OriginalString;
            return writer.WriteValueAsync(urlString);
        }
    }
}

