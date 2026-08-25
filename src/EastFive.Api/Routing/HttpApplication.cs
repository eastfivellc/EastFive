using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;

using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Mvc.Razor;

using EastFive.Linq;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Web;
using EastFive.Linq.Async;
using EastFive.Analytics;
using EastFive.Api.Core;
using EastFive.Api.Auth;
using EastFive.Api.Controllers;

namespace EastFive.Api
{
    public interface IApiApplication : IApplication
    {
        Type[] GetResources();

        string GetResourceMime(Type type);

        string GetResourceName(Type type);

        TResult GetResourceType<TResult>(string resourceType,
            Func<Type, TResult> onConverted,
            Func<TResult> onMatchingResourceNotFound);

        IEnumerableAsync<T> InstantiateAll<T>();

        IHostEnvironment HostEnvironment { get; }
    }

    [TextContentParser]
    [JsonContentParser]
    [XmlContentParser]
    [FormDataParser]
    [MimeMultipartContentParser]
    [ApiResources(NameSpacePrefixes = "EastFive.Api,EastFive.Web")]
    [Auth.ClaimEnableSession]
    [Auth.ClaimEnableActor]
    // request envelope deserializers — discovered via Attribute Interface
    // pattern by FunctionViewControllerAttribute.
    [Routing.Envelopes.QueryOnlyRequestEnvelope]
    [Routing.Envelopes.JsonRequestEnvelope]
    [Routing.Envelopes.FormRequestEnvelope]
    [Routing.Envelopes.MultipartRequestEnvelope]
    [Routing.Envelopes.RawRequestEnvelope]
    // Built-in IHandleMethodInvocation that runs the parameter-bound
    // validator pipeline (storage loaders, etc). Inherited by every
    // derived application; apps may add custom IHandleMethodInvocations
    // (cross-resource auth checks, additional parameter validators) at
    // the class or method scope alongside this one.
    [BoundParameterValidationHandler]
    public class HttpApplication : IApiApplication, IDescribeIsSecure, IDisposable
    {
        public virtual string Namespace
        {
            get
            {
                return "";
            }
        }

        protected IConfiguration configuration;

        #region Constructors / Destructors

        public HttpApplication(IConfiguration configuration)
        {
            this.configuration = configuration;
            initializationLock = new ManualResetEvent(false);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (initializationLock != null)
                {
                    initializationLock.Dispose();
                    initializationLock = null;
                }
                if (initialization != null)
                {
                    initialization.Dispose();
                    initialization = null;
                }
            }
        }

        ~HttpApplication()
        {
            Dispose(false);
        }

        #endregion

        #region Initialization

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostEnvironment env, IRazorViewEngine razorViewEngine)
        {
            this.HostEnvironment = env;
            app.Use(async (context, next) =>
            {
                var bodyControl = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpBodyControlFeature>();
                if (bodyControl != null)
                    bodyControl.AllowSynchronousIO = true;
                await next();
            });
            app.UseFVCRouting(this, this.configuration, razorViewEngine);
            ConfigureCallback(app, env, razorViewEngine);
        }

        protected virtual void ConfigureCallback(IApplicationBuilder app, IHostEnvironment env, IRazorViewEngine razorViewEngine)
        {
            LocateControllers();
            this.initialization = InitializeAsync();
        }

        private Task initialization;
        private ManualResetEvent initializationLock;

        protected bool IsInitialized => this.initialization.Status == TaskStatus.RanToCompletion;

        protected class Initialized
        {
            private Initialized()
            {

            }

            internal static Initialized Create()
            {
                return new Initialized();
            }
        }

        protected virtual Task<Initialized> InitializeAsync()
        {
            initializationLock.Set();
            return Initialized.Create().AsTask();
        }

        protected void InitializationWait()
        {
            initializationLock.WaitOne();
        }

        #endregion

        #region Logging

        public virtual EastFive.Analytics.ILogger Logger
        {
            get => new Analytics.ConsoleLogger();
        }

        public virtual TResult DoesStoreMonitoring<TResult>(
            Func<StoreMonitoringDelegate, TResult> onMonitorUsingThisCallback,
            Func<TResult> onNoMonitoring)
        {
            return onNoMonitoring();
        }

        #endregion

        #region MVC Initialization

        public void Application_Start()
        {
            ApplicationStart();
            Registration();
        }

        public virtual void ApplicationStart()
        {
            //SetupRazorEngine();
        }

        protected void Application_PreSendRequestHeaders()
        {
            PreSendRequestHeaders();
        }

        public virtual void PreSendRequestHeaders()
        {
        }

        protected virtual void Registration()
        {
        }

        #endregion

        #region Url Handlers

        public TResult GetResourceType<TResult>(string resourceType,
            Func<Type, TResult> onConverted,
            Func<TResult> onMatchingResourceNotFound)
        {
            return Resources
                .Where(
                    (resource) =>
                    {
                        if(resource.invokeResourceAttr.ContentType.HasBlackSpace())
                            if (resource.invokeResourceAttr.ContentType.Equals(resourceType.Trim(), StringComparison.OrdinalIgnoreCase))
                                return true;
                        if (resource.invokeResourceAttr.Route.HasBlackSpace())
                            if (resource.invokeResourceAttr.Route.Equals(resourceType.Trim(), StringComparison.OrdinalIgnoreCase))
                                return true;
                        if (resource.type.Name.HasBlackSpace())
                            if (resource.type.Name.Equals(resourceType.Trim(), StringComparison.OrdinalIgnoreCase))
                            return true;
                        return false;
                    })
                .First(
                    (res, next) => onConverted(res.type),
                    () => onMatchingResourceNotFound());
        }

        public string GetResourceMime(Type type)
        {
            if (type.IsDefaultOrNull())
                return "x-application/resource";
            return Resources
                .Where((resource) => resource.type == type)
                .First(
                    (resource, next) => resource.invokeResourceAttr.ContentType,
                    () => "x-application/resource");
        }

        public string GetResourceName(Type type)
        {
            if (type.IsDefaultOrNull())
                return string.Empty;
            return Resources
                .Where((resource) => resource.type == type)
                .First(
                    (resource, next) => resource.invokeResourceAttr.Route,
                    () => string.Empty);
        }

        public delegate Uri ControllerHandlerDelegate(
                HttpApplication httpApp, IProvideUrl urlHelper, ParameterInfo parameterInfo,
            Func<Type, string, Uri> onSuccess);


        public Type[] GetResources()
        {
            return Resources.Select(res => res.type).ToArray();
        }

        public TResult GetControllerType<TResult>(string routeName,
            Func<Type, TResult> onMethodsIdentified,
            Func<TResult> onKeyNotFound)
        {
            if (routeName.IsNullOrWhiteSpace())
                return onKeyNotFound();
            return Resources
                .Where((resource) => resource.invokeResourceAttr.Route.Equals(routeName, StringComparison.OrdinalIgnoreCase))
                .First(
                    (resource, next) => onMethodsIdentified(resource.type),
                    onKeyNotFound);
        }

        public Dictionary<Type, ControllerHandlerDelegate> urlHandlersByType =
            new Dictionary<Type, ControllerHandlerDelegate>()
            {
            };

        public Dictionary<string, ControllerHandlerDelegate> urlHandlersByTypeName =
            new Dictionary<string, ControllerHandlerDelegate>()
            {
            };

        #endregion

        #region Casts

        public object CastResourceProperty(object value, Type propertyType)
        {
            return this.CastResourceProperty(value, propertyType,
                (v) => v,
                () => throw new Exception());
        }

        public TResult CastResourceProperty<TResult>(object value, Type propertyType,
            Func<object, TResult> onCasted,
            Func<TResult> onNotMapped = default(Func<TResult>))
        {
            if (null == value)
            {
                var nullDefaultValue = propertyType.GetDefault();
                return onCasted(value);
            }

            var valueType = value.GetType();
            if (propertyType.IsAssignableFrom(valueType))
                return onCasted(value);

            if (typeof(IReferenceable).IsAssignableFrom(propertyType))
            {
                if (value is IReferenceable)
                {
                    var refValue = value as IReferenceable;
                    var guidIdValue = refValue.id;
                    return onCasted(guidIdValue);
                }
                if (value is Guid)
                {
                    var guidIdValue = (Guid)value;
                    return onCasted(guidIdValue);
                }
            }

            if (typeof(IReferenceableOptional).IsAssignableFrom(propertyType))
            {
                if (value is IReferenceableOptional)
                {
                    var refValue = value as IReferenceableOptional;
                    var guidIdMaybeValue = refValue.id;
                    return onCasted(guidIdMaybeValue);
                }
                if (value is Guid?)
                {
                    var guidIdMaybeValue = (Guid?)value;
                    return onCasted(guidIdMaybeValue);
                }
            }

            if (propertyType.IsAssignableFrom(typeof(string)))
            {
                if (value is Guid)
                {
                    var guidValue = (Guid)value;
                    var stringValue = guidValue.ToString();
                    return onCasted(stringValue);
                }
                if (value is IReferenceable)
                {
                    var refValue = value as IReferenceable;
                    var guidIdValue = refValue.id;
                    var stringValue = guidIdValue.ToString();
                    return onCasted(stringValue);
                }
                if (value is IReferenceableOptional)
                {
                    var refValue = value as IReferenceableOptional;
                    var guidIdMaybeValue = refValue.id;
                    if (!guidIdMaybeValue.HasValue)
                        return onCasted("null");

                    var stringValue = guidIdMaybeValue.Value.ToString();
                    return onCasted(stringValue);
                }
                if (value is bool)
                {
                    var boolValue = (bool)value;
                    var stringValue = boolValue.ToString();
                    return onCasted(stringValue);
                }
                if (value is DateTime)
                {
                    var dateValue = (DateTime)value;
                    var stringValue = dateValue.ToString();
                    return onCasted(stringValue);
                }
                if (value is DateTimeOffset)
                {
                    var dateValue = (DateTimeOffset)value;
                    var stringValue = dateValue.ToString();
                    return onCasted(stringValue);
                }
            }

            if (onNotMapped.IsDefaultOrNull())
                throw new Exception($"Cannot create {propertyType.FullName} from {value.GetType().FullName}");
            return onNotMapped();
        }


        #endregion

        #region Load Controllers

        private IDictionary<Type, ResourceInvocation> routeResourceExtensionLookup
            = new Dictionary<Type, ResourceInvocation>();

        public ResourceInvocation[] Resources => routeResourceExtensionLookup
            .SelectValues().ToArray();

        public IDictionary<Type, ConfigAttribute> ConfigurationTypes => configurationTypes;

        public IHostEnvironment HostEnvironment { get; private set; }

        public static IDictionary<Type, ConfigAttribute> configurationTypes;

        private void LocateControllers()
        {
            object lookupLock = new object();

            var limitedAssemblyQuery = this.GetType()
                .GetAttributesInterface<IApiResources>(inherit: true, multiple: true);

            AppDomain.CurrentDomain.AssemblyLoad += (object sender, AssemblyLoadEventArgs args) =>
            {
                var check = ShouldCheckAssembly(args.LoadedAssembly);
                if (!check)
                    return;
                lock (lookupLock)
                {
                    AddControllersFromAssembly(args.LoadedAssembly);
                }
            };

            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(ShouldCheckAssembly)
                .ToArray();

            lock (lookupLock)
            {
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

        private void AddControllersFromAssembly(System.Reflection.Assembly assembly)
        {
            try
            {
                foreach(var type in assembly.GetTypes())
                {
                    var invokeResourceAttrs = type.GetAttributesInterface<IInvokeResource>();
                    if (invokeResourceAttrs.Any())
                    {
                        var invokeResourceAttr = invokeResourceAttrs.First();
                        if (!routeResourceExtensionLookup.ContainsKey(type))
                        {
                            routeResourceExtensionLookup.Add(type,
                                new ResourceInvocation
                                {
                                    type = type,
                                    invokeResourceAttr = invokeResourceAttr,
                                });
                        }
                        else
                        {
                            routeResourceExtensionLookup[type].invokeResourceAttr = invokeResourceAttr;
                        }
                    }
                    var invokeExtensionsAttrs = type.GetAttributesInterface<IInvokeExtensions>();
                    if (invokeExtensionsAttrs.Any())
                    {
                        var invokeExtensionsAttr = invokeExtensionsAttrs.First();
                        var extensionMethods = invokeExtensionsAttr.GetResourcesExtended(type);
                        foreach (var extensionsKvp in extensionMethods)
                        {
                            var extendedType = extensionsKvp.Key;
                            if (!routeResourceExtensionLookup.ContainsKey(extendedType))
                            {
                                routeResourceExtensionLookup.Add(extendedType,
                                    new ResourceInvocation
                                    {
                                        type = extendedType,
                                    });
                            }
                            else
                            {
                                routeResourceExtensionLookup[extendedType].extensions =
                                    routeResourceExtensionLookup[extendedType].extensions
                                    .NullToEmpty()
                                    .Append(extensionsKvp.Value)
                                    .ToArray();
                            }
                        }
                    }
                    if (type.ContainsCustomAttribute<ConfigAttribute>())
                    {
                        var attr = type.GetCustomAttribute<ConfigAttribute>();
                        configurationTypes = configurationTypes
                            .NullToEmpty()
                            .Append(attr.PairWithKey(type))
                            .Distinct(ct => ct.Key.FullName)
                            .ToDictionary();
                    }
                }
            }
            catch (System.Reflection.ReflectionTypeLoadException ex)
            {
                ex.GetType();
            }
            catch (Exception ex)
            {
                ex.GetType();
            }
        }

        #endregion

        #region Instigators

        #region Instigators - GENERIC

        public Dictionary<Type, InstigatorDelegateGeneric> instigatorsGeneric =
            new Dictionary<Type, InstigatorDelegateGeneric>()
            {
            };

        public void SetInstigatorGeneric(Type type, InstigatorDelegateGeneric instigator, bool clear = false)
        {
            instigatorsGeneric[type] = instigator;
        }

        #endregion

        #region Instigators - Concrete

        public const string DiagnosticsLogProperty = "X-Diagnostics-Log";

        protected Dictionary<Type, InstigatorDelegate> instigators =
            new Dictionary<Type, InstigatorDelegate>()
            {
                #region Security

                {
                    typeof(Controllers.ApiSecurity),
                    (httpApp, routeData, paramInfo, success) =>
                    {
                        return EastFive.Web.Configuration.Settings.GetString(AppSettings.ApiKey,
                            (authorizedApiKey) =>
                            {
                                var queryParams = System.Web.HttpUtility.ParseQueryString(routeData.GetAbsoluteUri().Query);
                                if (queryParams[ApiSecurityAttribute.apiKeySecurity] == authorizedApiKey)
                                    return success(new Controllers.ApiSecurity());

                                var authorization = routeData.GetAuthorization();

                                if(authorization == authorizedApiKey)
                                    return success(new Controllers.ApiSecurity());
                                if(authorization == authorizedApiKey)
                                    return success(new Controllers.ApiSecurity());

                                return routeData.CreateResponse(HttpStatusCode.Unauthorized).AsTask();
                            },
                            (why) => routeData.CreateResponse(HttpStatusCode.Unauthorized).AddReason(why).AsTask());
                    }
                },

                #endregion

                #region MVC System Objects

                

                #endregion

                #region Logging

                {
                    typeof(Analytics.ILogger),
                    async (httpApp, request, paramInfo, success) =>
                    {
                        if(!request.TryGetHeader("X-Diagnostics", out string hdrValue))
                            return await success(httpApp.Logger);
                        var timer = new Stopwatch();
                        timer.Start();
                        var logger = new Analytics.CaptureLog(httpApp.Logger, timer);
                        var response = await success(logger);
                        logger.Trace("Response concluded.");
                        var diagnosticsLog = logger.Dump();
                        response.Request.Properties.Add(DiagnosticsLogProperty, diagnosticsLog);
                        // response.Content = new StringContent(responseString, Encoding.UTF8, "text/text");
                        return response;
                    }
                },

                #endregion

            };

        public void AddInstigator(Type type, InstigatorDelegate instigator)
        {
            instigators.Add(type, instigator);
        }

        public void SetInstigator(Type type, InstigatorDelegate instigator, bool clear = false)
        {
            instigators[type] = instigator;
        }

        public Task<IHttpResponse> Instigate(IHttpRequest request, ParameterInfo methodParameter,
            Func<object, Task<IHttpResponse>> onInstigated)
        {
            #region Check for app level override 

            var instigationAttrsApp = this.GetType()
                .GetAttributesInterface<IInstigate>()
                .Where(instigator => instigator.CanInstigate(methodParameter));
            if (instigationAttrsApp.Any())
            {
                var instigationAttr = instigationAttrsApp.First();
                return instigationAttr.Instigate(this,
                        request, methodParameter,
                    onInstigated);
            }

            var instigationGenericAttrsApp = this.GetType()
                .GetAttributesInterface<IInstigateGeneric>()
                .Where(instigator => instigator.CanInstigate(methodParameter));
            if (instigationGenericAttrsApp.Any())
            {
                var instigationAttr = instigationGenericAttrsApp.First();
                return instigationAttr.InstigatorDelegateGeneric(methodParameter.ParameterType, this,
                        request, methodParameter,
                    (v) => onInstigated(v));
            }

            #endregion

            #region Type attributes for instigation

            var instigationAttrs = methodParameter.ParameterType.GetAttributesInterface<IInstigatable>();
            if (instigationAttrs.Any())
            {
                var instigationAttr = instigationAttrs.First();
                return instigationAttr.Instigate(this,
                        request, methodParameter,
                    onInstigated);
            }

            var instigationGenericAttrs = methodParameter.ParameterType.GetAttributesInterface<IInstigatableGeneric>();
            if (instigationGenericAttrs.Any())
            {
                var instigationAttr = instigationGenericAttrs.First();
                return instigationAttr.InstigatorDelegateGeneric(methodParameter.ParameterType, this,
                        request, methodParameter,
                    (v) => onInstigated(v));
            }

            #endregion

            #region Context types

            if (methodParameter.ParameterType.IsAssignableFrom(typeof(CancellationToken)))
                return onInstigated(request.CancellationToken);

            if (methodParameter.ParameterType.IsAssignableFrom(typeof(IHttpRequest)))
                return onInstigated(request);

            #endregion

            if (this.instigators.ContainsKey(methodParameter.ParameterType))
                return this.instigators[methodParameter.ParameterType](this, request, methodParameter,
                    (v) => onInstigated(v));

            if (methodParameter.ParameterType.IsGenericType)
            {
                var possibleGenericInstigator = this.instigatorsGeneric
                    .Where(instigatorKvp => instigatorKvp.Key.GUID == methodParameter.ParameterType.GUID)
                    .ToArray();
                if (possibleGenericInstigator.Any())
                    return possibleGenericInstigator.First().Value(methodParameter.ParameterType,
                        this, request, methodParameter,
                    (v) => onInstigated(v));
            }

            if (methodParameter.ParameterType.IsInstanceOfType(this))
                return onInstigated(this);

            return request.CreateResponse(HttpStatusCode.InternalServerError)
                .AddReason($"Could not instigate type: {methodParameter.ParameterType.FullName}. Please add an instigator for that type.")
                .AsTask();
        }

        #endregion

        #endregion

        #region Instantiations
        
        public delegate Task<object> InstantiationDelegate(HttpApplication httpApp);

        protected Dictionary<Type, InstantiationDelegate> instantiations =
            new Dictionary<Type, InstantiationDelegate>();

        public delegate Task<object> InstantiationGenericDelegate(Type type, HttpApplication httpApp);

        protected Dictionary<Type, InstantiationGenericDelegate> instantiationsGeneric =
            new Dictionary<Type, InstantiationGenericDelegate>()
            {
            };

        internal bool CanInstantiate(Type type)
        {
            if (this.instantiations.ContainsKey(type))
                return true;

            if (type.IsGenericType)
            {
                var possibleGenericInstantiator = this.instantiationsGeneric
                    .Where(
                        instigatorKvp =>
                        {
                            if (instigatorKvp.Key.GUID == type.GUID)
                                return true;
                            if (type.IsAssignableFrom(instigatorKvp.Key))
                                return true;
                            return false;
                        });
                return possibleGenericInstantiator.Any();
            }

            return false;
        }

        public async Task<TResult> InstantiateAsync<TResult>(Type type,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind)
        {
            if (type.IsGenericType)
            {
                var possibleGenericInstantiator = this.instantiationsGeneric
                    .Where(
                        instigatorKvp =>
                        {
                            if (instigatorKvp.Key.GUID == type.GUID)
                                return true;
                            if (type.IsAssignableFrom(instigatorKvp.Key))
                                return true;
                            return false;
                        });
                if (possibleGenericInstantiator.Any())
                {
                    var resultBound = await possibleGenericInstantiator.First().Value(type, this);
                    var castResult = (TResult)resultBound;
                    return castResult;
                }
            }

            var possibleInstantiators = this.instantiations
                .Where(
                    intatiatorKvp =>
                    {
                        var instantiatorType = intatiatorKvp.Key;
                        if (instantiatorType.IsSubClassOfGeneric(type))
                            return true;
                        return false;
                    });
            if (possibleInstantiators.Any())
            {
                var instance = await possibleInstantiators.First().Value(this);
                var castedVal = onParsed(instance);
                return castedVal;
            }

            return onDidNotBind($"No binding for type `{type.FullName}` active in server.");
        }

        public IEnumerableAsync<T> InstantiateAll<T>()
        {
            var type = typeof(T);
            return this.instantiations
                .Where(
                    instigatorKvp =>
                    {
                        if (instigatorKvp.Key.GUID == type.GUID)
                            return true;
                        if (type.IsAssignableFrom(instigatorKvp.Key))
                            return true;
                        if(instigatorKvp.Key.IsInterface)
                            if (instigatorKvp.Key.IsSubClassOfGeneric(type))
                                return true;
                        return false;
                    })
                .Select(
                    async instantiator =>
                    {
                        var resultBound = await instantiator.Value(this);
                        return resultBound;
                    })
                .Concat(
                    this.instantiationsGeneric
                    .Where(
                        instigatorKvp =>
                        {
                            if (instigatorKvp.Key.GUID == type.GUID)
                                return true;
                            if (type.IsAssignableFrom(instigatorKvp.Key))
                                return true;
                            return false;
                        })
                    .Select(
                        async instantiator =>
                        {
                            var resultBound = await instantiator.Value(type, this);
                            return resultBound;
                        }))
                .AsyncEnumerable()
                .Where(v => v is T)
                .Select(
                    instantiationResult =>
                    {
                        return (T)instantiationResult;
                    });
        }

        public void AddOrUpdateInstantiation(Type type, InstantiationDelegate instantiation)
        {
            if (instantiations.ContainsKey(type))
                instantiations[type] = instantiation;
            else
                instantiations.Add(type, instantiation);
        }

        public void AddOrUpdateGenericInstantiation(Type type, InstantiationGenericDelegate instantiation)
        {
            if (this.instantiationsGeneric.ContainsKey(type))
                this.instantiationsGeneric[type] = instantiation;
            else
                this.instantiationsGeneric.Add(type, instantiation);
        }

        #endregion

        #region Conversions

        public IEnumerable<MethodInfo> GetExtensionMethods(Type controllerType)
        {
            if (routeResourceExtensionLookup.ContainsKey(controllerType))
                return routeResourceExtensionLookup[controllerType].extensions.NullToEmpty();
            return new MethodInfo[] { };
        }

        #endregion

        public virtual bool IsSecurityAttribute(Attribute attr)
        {
            bool isAccessKey()
            {
                var qry = attr as QueryValidationAttribute;
                if (qry != null && qry.Name == ApiSecurityAttribute.accessKey)
                    return true;

                return false;
            }

            return attr is RequiredClaimAttribute ||
                attr is SecurityRoleRequiredAttribute ||
                attr is UnsecuredAttribute ||
                isAccessKey();
        }

        public virtual bool IsSecurityParameter(System.Reflection.ParameterInfo parameter)
        {
            return parameter.GetCustomAttributes()
                .Any(attr => IsSecurityAttribute(attr));
        }
    }
}
