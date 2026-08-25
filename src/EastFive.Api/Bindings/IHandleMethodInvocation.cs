using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace EastFive.Api
{
    /// <summary>
    /// Continuation handed to an <see cref="IHandleMethodInvocation"/>.
    /// Invoking it runs the next handler in the chain, eventually
    /// terminating in the controller method invocation. Handlers either
    /// invoke this delegate (optionally with mutated parameters or
    /// binding contexts) or short-circuit by returning their own response.
    /// </summary>
    public delegate Task<IHttpResponse> InvokeMethodDelegate(
        KeyValuePair<ParameterInfo, object>[] parameters,
        IReadOnlyDictionary<ParameterInfo, object> bindingContexts,
        MethodInfo method, IApplication httpApp, IHttpRequest request);

    /// <summary>
    /// Sequential method-invocation handler. Discovered from the
    /// application class, the controller method, and each bound
    /// parameter's type (Attribute Interface pattern). Composed into a
    /// chain whose terminal invokes the controller method.
    ///
    /// One interface, two roles:
    ///  - Cross-cutting wrappers (App Insights timing/telemetry, custom
    ///    logging) sit at the application level. They just await
    ///    <c>continueInvocation</c> and observe the result.
    ///  - Authorization gates (<c>[RequiredClaim]</c>, role checks,
    ///    <c>[Unsecured]</c>) sit on methods or parameter types. They
    ///    short-circuit with a 403/401 response when the gate fails;
    ///    otherwise they delegate to <c>continueInvocation</c>.
    ///
    /// Parameter validation handlers — including the framework's built-in
    /// <see cref="BoundParameterValidationHandlerAttribute"/> — are also
    /// <see cref="IHandleMethodInvocation"/>s. They can mutate the
    /// parameter list (replace a slot value) and/or
    /// <c>bindingContexts</c> before delegating, allowing downstream
    /// handlers and the controller to see the mutated values.
    /// Application-defined parameter validators (e.g. cross-resource
    /// authorization checks like "this account's practice must be in the
    /// caller's allow-list") follow the same pattern.
    ///
    /// Discovery order (= run order; outermost handler runs first,
    /// innermost wraps the invocation):
    ///   1. Application-class handlers (<c>httpApp.GetType()</c>)
    ///   2. Controller-method handlers (<c>chosen.Method</c>)
    ///   3. Parameter-type handlers (each bound parameter's type)
    /// </summary>
    public interface IHandleMethodInvocation
    {
        /// <summary>
        /// Wrap the next link in the chain. Either await
        /// <paramref name="continueInvocation"/> (optionally mutating
        /// parameters or binding contexts beforehand) or return a
        /// short-circuit response without calling it.
        /// </summary>
        /// <param name="parameters">
        /// Live parameter slot list. The list passed to
        /// <paramref name="continueInvocation"/> is what the next handler
        /// (and ultimately the controller) will see.
        /// </param>
        /// <param name="bindingContexts">
        /// Per-parameter context dictionary populated during bind by
        /// <c>IProvideBindingRequirements.GetParameterBinding</c>'s
        /// assemble closure. Keys are owning parameters; entries may be
        /// absent for parameters that did not produce context. Pass
        /// through unchanged in the common case.
        /// </param>
        Task<IHttpResponse> HandleMethodInvocationAsync(
            KeyValuePair<ParameterInfo, object>[] parameters,
            IReadOnlyDictionary<ParameterInfo, object> bindingContexts,
            MethodInfo method, IApplication httpApp, IHttpRequest request,
            InvokeMethodDelegate continueInvocation);
    }
}
