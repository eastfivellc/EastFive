using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using EastFive.Api.Bindings.ContentHandlers;
using EastFive.Linq;
using EastFive.Reflection;

namespace EastFive.Api
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class HtmlAttribute : System.Attribute, IProvideSerialization
    {
        public HtmlAttribute()
        {
        }

        public string MediaType => "text/html";

        public string Title { get; set; }

        public string ContentType => MediaType;

        private const double defaultPreference = -111;

        public double Preference { get; set; } = defaultPreference;

        public double GetPreference(IHttpRequest request)
        {
            if (Preference != defaultPreference)
                return Preference;

            var accepts = request.GetAcceptTypes();
            var matches = accepts.Where(mt => mt.MediaType.ToLower() == this.MediaType);
            if (matches.Any())
            {
                var match = matches.First();
                if (match.Quality.HasValue)
                    return match.Quality.Value;
                return 0.5;
            }

            if (accepts.Where(mt => mt.MediaType.ToLower().Contains("html")).Any())
                return 0.1;

            return -1.0;
        }

        public async Task SerializeAsync(Stream responseStream,
            IApplication httpApp, IHttpRequest request, ParameterInfo paramInfo, object obj)
        {
            var contentType = this.MediaType;

            var urlHelper = new UrlBuilder(request);

            var properties = obj.GetType()
                .GetMembers()
                .Where(member => member.ContainsAttributeInterface<IRenderHtml>())
                .SelectMany(
                    member =>
                    {
                        return member
                            .GetAttributesInterface<IRenderHtml>()
                            .Select(
                                attr =>
                                {
                                    return (string)attr.GetType()
                                        .GetMethod("RenderHtml")
                                        .MakeGenericMethod(obj.GetType())
                                        .Invoke(attr, new object[] { obj, member, httpApp, request, urlHelper });
                                });
                    })
                .Join("\n");
            var head = $"<head><title>{this.Title}</title><script href=\"/Content/Renderers/Html/RenderHtml.js\" /></head>";
            var body = $"<body><form action=\"{(obj as IReferenceable).id}\">{properties}</form></body>";
            var html = $"<html>{head}<body>{body}</body></html>";

            await responseStream.WriteResponseText(html, request);
        }
    }

    public interface IRenderHtml
    {
        string RenderHtml<T>(T obj, MemberInfo member,
            HttpApplication httpApp, HttpRequestMessage request, IBuildUrls urlBuilder);
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class HtmlInputAttribute : System.Attribute
    {
        public HtmlInputAttribute()
        {
        }
        
        private string label;
        public string Label
        {
            get
            {
                return this.label;
            }
            set
            {
                label = value;
            }
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class HtmlInputHiddenAttribute : HtmlInputAttribute
    {
        public HtmlInputHiddenAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class HtmlLinkAttribute : HtmlInputAttribute, IRenderHtml
    {
        public HtmlLinkAttribute()
        {
        }

        public string RenderHtml<T>(T obj, MemberInfo member,
            HttpApplication httpApp, HttpRequestMessage request,
            IBuildUrls urlBuilder)
        {
            var type = member.GetPropertyOrFieldType();
            
            //if (type.IsSubClassOfGeneric(typeof(IRef<>)))
            //{
            //    var refdType = type.GenericTypeArguments.First();
            //    var id = (Guid)member.GetPropertyOrFieldValue(obj);
            //    var singleResourceQuery1 =
            //        from res in urlBuilder.Resources<IReferenceable>()
            //        where res.id == id
            //        select res;

            //    var singleResourceQuery2 = urlBuilder
            //        .Resources<IReferenceable>()
            //        .Where(res => res.id == id)
            //        .Location();

            //    return $"<a href=\"/{refdType.Name}/{id}\">{this.Label}</a>";
            //}

            //if (type.IsSubClassOfGeneric(typeof(IRefOptional<>)))
            //{
            //    var refdType = type.GenericTypeArguments.First();
            //    var idRef = member.GetPropertyOrFieldValue(obj) as IReferenceableOptional;
            //    var singleResourceQuery1 =
            //        from res in urlBuilder.Resources<IReferenceable>()
            //        where res.id == idRef.id
            //        select res;

            //    var singleResourceQuery2 = urlBuilder
            //        .Resources<IReferenceable>()
            //        .Where(res => res.id == idRef.id)
            //        .Location();

            //    return $"<a href=\"/{refdType.Name}/{idRef.id}\">{this.Label}</a>";
            //}

            return $"<a href=\"unknown\">{this.Label}</a>";
        }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
    public class HtmlActionAttribute : System.Attribute, IRenderHtml
    {
        public HtmlActionAttribute()
        {
        }

        private string label;
        public string Label
        {
            get
            {
                return this.label;
            }
            set
            {
                label = value;
            }
        }

        public string RenderHtml<T>(T obj, MemberInfo member,
            HttpApplication httpApp, HttpRequestMessage request,
            IBuildUrls urlBuilder)
        {
            var actionString = member.GetCustomAttributes<HttpVerbAttribute>(true)
                .First(
                    (verb, next) => verb.Method,
                    () => "Get");
            return $"<a href=\"javascript:{actionString.ToLower()}Resource()\">{this.Label}</a>";
        }
    }
}
