using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Resources
{
    public interface IDocumentMethod
    {
        Method GetMethod(Route route, MethodInfo method, HttpApplication httpApp);
    }

    public class Method
    {
        public Method(string httpMethod, MethodInfo methodInfo, Route route, Uri path, HttpApplication httpApp)
        {
            this.MethodPoco = methodInfo;
            this.Route = route;
            this.HttpMethod = httpMethod;
            this.Name = methodInfo.Name;
            this.Path = path;
            this.Description = methodInfo.GetCustomAttribute<System.ComponentModel.DescriptionAttribute, string>(
                (attr) => attr.Description,
                () => string.Empty);
            this.Parameters = methodInfo.GetParameters()
                .Where(methodParam => methodParam.ContainsAttributeInterface<IDocumentParameter>(true))
                .Select(methodParam => methodParam
                    .GetAttributeInterface<IDocumentParameter>(true)
                    .GetParameter(methodParam, httpApp))
                .ToArray();
            this.Responses = methodInfo.GetParameters()
                .Where(
                    methodParam =>
                    {
                        if (methodParam.ParameterType.ContainsAttributeInterface<IDocumentResponse>())
                            return true;
                        var isDelegate = typeof(MulticastDelegate).IsAssignableFrom(methodParam.ParameterType);
                        if (isDelegate)
                            return true;
                        return false;
                    })
                .Select(
                    methodParam =>
                    {
                        var attrs = methodParam.ParameterType.GetAttributesInterface<IDocumentResponse>();
                        if (attrs.Any())
                        {
                            var attr = attrs.First();
                            return attr.GetResponse(methodParam, httpApp);
                        }
                        var isDelegate = typeof(MulticastDelegate).IsAssignableFrom(methodParam.ParameterType);
                        if (isDelegate)
                            return new Response(methodParam);
                        throw new Exception();
                    })
                .ToArray();
        }

        public MethodInfo MethodPoco { get; set; }

        public string HttpMethod { get; set; }

        public Route Route { get; set; }

        public string Name { get; set; }

        public Uri Path { get; set; }

        public string Description { get; set; }

        public Parameter[] Parameters { get; set; }

        public Response[] Responses { get; set; }
    }
}
