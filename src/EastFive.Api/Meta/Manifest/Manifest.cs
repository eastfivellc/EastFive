using EastFive.Api.Auth;
using EastFive.Api.Meta.OpenApi;
using EastFive.Extensions;
using EastFive.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace EastFive.Api.Resources
{
    [FunctionViewController(Route = "Manifest")]
    [OpenApiRoute(Collection = "EastFive.Api.Meta")]
    public class Manifest
    {

        public Manifest(IEnumerable<Type> lookups,
            HttpApplication httpApp)
        {
            this.Routes = lookups
                .Where(type => type.ContainsAttributeInterface<IDocumentRoute>())
                .Select(type => type.GetAttributesInterface<IDocumentRoute>()
                    .First()
                    .GetRoute(type, httpApp))
                .OrderBy(route => route.Name)
                .ToArray();
        }

        public Route[] Routes { get; set; }

        [SecurityRoleRequired(RolesAllowed = new[]
        {
            ClaimValues.Roles.SecurityReaderRoleId,
            ClaimValues.Roles.SecurityReaderRole,
            ClaimValues.Roles.SuperAdmin,
        })]
        [HttpAction("Security")]
        public static IHttpResponse GetAttributes(
                [OptionalQueryParameter(Name = "untrusted_only")] bool? untrustedOnly,
                [OptionalQueryParameter(Name = "summary_only")] bool? summaryOnly,
                HttpApplication application, 
                IHttpRequest request, 
                IProvideUrl url,
            JsonStringResponse onJson)
        {
            var lookups = application.GetResources();
            var manifest = new EastFive.Api.Resources.Manifest(lookups, application);
            var result = manifest.Routes
                .SelectMany(route => route.Methods.NullToEmpty())
                .OrderBy(method => method.Path.ToString())
                .Select(method =>
                {
                    var attrs = method.MethodPoco.DeclaringType
                        .GetCustomAttributes()
                        .Concat(method.MethodPoco.GetCustomAttributes())
                        .ToArray();
                    // Collected rather than merely tested. The same list answers all three
                    // questions an audit asks -- is it gated, by WHAT, and if it is open, why --
                    // where `.Any(...)` answered only the first and a consumer was left showing
                    // one undifferentiated "secured" chip over everything from a SuperAdminClaim
                    // to a webhook shared secret.
                    var securityAttrs = attrs
                        .Where(attr => application.IsSecurityAttribute(attr))
                        .ToArray();
                    var hasSecAttribute = securityAttrs.Any();
                    // Collected for the same reason as the attributes above, and it matters MORE
                    // here: "secured by a parameter" covers two opposite things. A `SessionToken`
                    // fails to bind without a valid Authorization header (401), so it really is
                    // authentication; a `SessionTokenMaybe` binds to a null sessionId when the
                    // header is absent, so it gates nothing at all. Reporting only the boolean
                    // made those identical to every consumer.
                    var securityParams = method.MethodPoco
                        .GetParameters()
                        .Where(
                            (param) =>
                            {
                                // [Resource] makes any attribute behave CRUD-like so it skips over any security check
                                var isResource = param.GetCustomAttributes()
                                    .Any(attr => attr is ResourceAttribute);

                                return !isResource && application.IsSecurityParameter(param);
                            })
                        .ToArray();
                    var hasSecParameter = securityParams.Any();
                    // Read off `attrs`, not off `securityAttrs`: [Unsecured] counts as a security
                    // attribute only because the BASE IsSecurityAttribute says so, and that method
                    // is virtual. Deriving the deliberate-open flag from the registry would let an
                    // application that overrides it stop reporting its own open routes.
                    var unsecured = attrs
                        .OfType<UnsecuredAttribute>()
                        .ToArray();
                    var isUnsecured = unsecured.Any();
                    var needsFurtherEvaluation = !hasSecAttribute && !hasSecParameter;
                    if ((untrustedOnly ?? false) && !needsFurtherEvaluation)
                        return null;

                    return new
                    {
                        verb = method.HttpMethod,
                        endpoint = method.Path.ToString(),
                        method = method.MethodPoco.DeclaringType.Namespace + "." + method.Route.Name + "." + method.Name,
                        // The ASSEMBLY, not the namespace prefix of `method` above -- they do not
                        // agree. EastFive.Azure.dll already declares controllers namespaced
                        // EastFive.Api.Azure.Apple and EastFive.Apple, so a consumer partitioning
                        // "the app's endpoints" from "the framework's" by name is guessing.
                        assembly = method.MethodPoco.DeclaringType.Assembly.GetName().Name,
                        // Which gate, spelled as the author wrote it. Empty when the row is
                        // secured by a PARAMETER rather than an attribute -- that difference is
                        // the point: a parameter is a binding, not a check.
                        gate = securityAttrs
                            .Select(attr => GateName(attr))
                            .Distinct()
                            .Join(", "),
                        // Which parameter did it -- the distinction the boolean above cannot make.
                        // The TYPE name, which is what the registry keys on for all of
                        // SessionToken/SessionTokenMaybe/Security/Authorization and an
                        // application's own additions. A parameter that qualifies by carrying a
                        // security ATTRIBUTE instead is still named by its type here, which
                        // identifies the parameter without naming what satisfied it.
                        parameterGate = securityParams
                            .Select(param => param.ParameterType.Name)
                            .Distinct()
                            .Join(", "),
                        // [Unsecured] demands a reason at construction; without this the audit
                        // showed 65 identical chips and threw every authored justification away.
                        unsecuredReason = unsecured
                            .Select(attr => attr.Reason)
                            .FirstOrDefault(),
                        secAttribute = hasSecAttribute ? 1 : 0, // more csv friendly than boolean
                        secParameter = hasSecParameter ? 1 : 0,
                        isUnsecured = isUnsecured ? 1 : 0,
                    };
                })
                .Where(obj => obj != null);
            if (summaryOnly ?? false)
            {
                var summary = new
                {
                    number_of_endpoints = result.Count(),
                    untrusted_endpoints = result.Where(r => r.secAttribute == 0 && r.secParameter == 0).Count(),
                    open_flow_endpoints = result.Where(r => r.isUnsecured == 1).Count(),
                    verb_summary = result
                        .GroupBy(r => r.verb.ToUpper())
                        .Select(g => new
                        {
                            verb = g.Key,
                            count = g.Count(),
                        }),
                };
                return onJson(JsonConvert.SerializeObject(summary, Formatting.Indented));
            }
            return onJson(JsonConvert.SerializeObject(result, Formatting.Indented));
        }

        /// <summary>
        /// An attribute's name as an author writes it -- <c>CustomerServiceClaim</c>, not
        /// <c>CustomerServiceClaimAttribute</c> -- so an audit reads back like the source it
        /// describes and a reader can grep for what they see.
        /// </summary>
        private static string GateName(Attribute attr)
        {
            const string suffix = "Attribute";
            var name = attr.GetType().Name;
            return name.EndsWith(suffix) && name.Length > suffix.Length
                ? name.Substring(0, name.Length - suffix.Length)
                : name;
        }

        public static IHttpResponse HtmlContent(
                HttpApplication httpApp, IHttpRequest request, IProvideUrl url,
            ViewFileResponse<Api.Resources.Manifest> onHtml)
        {
            var lookups = httpApp.GetResources();
            var manifest = new EastFive.Api.Resources.Manifest(lookups, httpApp);
            return onHtml("Manifest/Manifest.cshtml", manifest);
        }

        public static string GetRouteHtml(string route, KeyValuePair<System.Net.Http.HttpMethod, MethodInfo[]>[] methods)
        {
            var html = methods
                .Select(methodKvp => $"<div><h4>{methodKvp.Key}</h4>{GetMethodHtml(methodKvp.Key.Method, methodKvp.Value)}</div>")
                .Join("");
            return html;
        }

        public static string GetMethodHtml(string httpVerb, MethodInfo[] methods)
        {
            var html = methods
                .Select(
                    method =>
                    {
                        var parameterHtml = method
                            .GetParameters()
                            .Where(methodParam => methodParam.ContainsAttributeInterface<IBindApiValue>(true))
                            .Select(
                                methodParam =>
                                {
                                    var validator = methodParam.GetAttributeInterface<IBindApiValue>();
                                    var lookupName = validator.GetKey(methodParam);
                                    var required = methodParam.ContainsCustomAttribute<PropertyAttribute>() ||
                                        methodParam.ContainsCustomAttribute<QueryParameterAttribute>();

                                    return CSharpInvocationHtml(lookupName, required, methodParam.ParameterType);

                                })
                            .Join(",");
                        return $"<span class=\"method,csharp\">{method.Name}({parameterHtml})</span>";
                    })
                .Join("");
            return html;
        }

        public static string CSharpInvocationHtml(string name, bool required, Type parameterType)
        {
            var requiredString = required ? "[Required]" : "[Optional]";
            return $"<span>[{requiredString}]{parameterType.Name} <span>{name}</span></span>";
        }

        #region Load Controllers

        private static object lookupLock = new object();
        private static Type[] lookup;

        private static void LocateControllers(Type applicationType)
        {
            var limitedAssemblyQuery = applicationType
                .GetAttributesInterface<IApiResources>(inherit: true, multiple: true);

            lock (lookupLock)
            {
                if (!Manifest.lookup.IsDefaultNullOrEmpty())
                    return;

                AppDomain.CurrentDomain.AssemblyLoad += (object sender, AssemblyLoadEventArgs args) =>
                {
                    lock (lookupLock)
                    {
                        AddControllersFromAssembly(args.LoadedAssembly);
                    }
                };

                var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(ShouldCheckAssembly)
                    .ToArray();

                foreach (var assembly in loadedAssemblies)
                {
                    AddControllersFromAssembly(assembly);
                }
            }

            bool ShouldCheckAssembly(Assembly assembly)
            {
                return limitedAssemblyQuery
                    .First(
                        (limitedAssembly, next) =>
                        {
                            if (limitedAssembly.ShouldCheckAssembly(assembly))
                                return true;
                            return next();
                        },
                        () => false);
            }
        }


        private static void AddControllersFromAssembly(System.Reflection.Assembly assembly)
        {
            try
            {
                var types = assembly
                    .GetTypes();
                var results = types
                    .Where(type =>
                        type.GetCustomAttribute<FunctionViewControllerAttribute, bool>((attrs) => true, () => false))
                    .ToArray();

                Manifest.lookup = Manifest.lookup.NullToEmpty()
                    .Concat(results)
                    .Distinct(r => r.GUID)
                    .ToArray();
            }
            catch (Exception ex)
            {
                ex.GetType();
            }
        }

        #endregion
    }

    public interface IProvideResponseType
    {
        Type GetResponseType(ParameterInfo parameterInfo);
    }

    public class Response
    {
        public Response(ParameterInfo paramInfo)
        {
            this.ParamInfo = paramInfo;
            this.Name = paramInfo.Name;
            this.StatusCode = System.Net.HttpStatusCode.OK;
            //this.Example = "TODO: JSON serialize response type";
            this.Headers = new KeyValuePair<string, string>[] { };
        }

        public Response()
        {
        }

        public ParameterInfo ParamInfo { get; set; }

        public string Name { get; set; }

        public System.Net.HttpStatusCode StatusCode { get; set; }

        public string Example { get; set; }

        public KeyValuePair<string, string>[] Headers { get; set; }

        public bool IsMultipart { get; set; }
    }
}
