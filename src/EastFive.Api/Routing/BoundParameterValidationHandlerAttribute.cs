using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using EastFive.Api.Bindings;
using EastFive.Linq;

namespace EastFive.Api
{
    /// <summary>
    /// Built-in <see cref="IHandleMethodInvocation"/> that runs the
    /// parameter-bound validator pipeline:
    ///   - Discover every <see cref="IValidateHttpRequestForBoundParameters"/>
    ///     attached to the controller method, each bound parameter's type,
    ///     and each bound parameter's own attributes.
    ///   - Launch their async pre-work in parallel.
    ///   - Compose the returned <see cref="ParameterMutation"/> closures
    ///     into a sequential chain whose terminal hands the
    ///     (possibly mutated) parameter list back to the next
    ///     <see cref="IHandleMethodInvocation"/> in the outer chain.
    ///
    /// Default-applied to <see cref="HttpApplication"/> (and inherited by
    /// every derived application), so apps get today's storage-loader
    /// behaviour out of the box. Apps may also apply this attribute (or
    /// custom <see cref="IHandleMethodInvocation"/>s) at the class or
    /// method scope to add additional parameter-validation handlers
    /// (e.g. cross-resource authorization checks).
    /// </summary>
    /// <remarks>
    /// A local <see cref="CancellationTokenSource"/> linked to
    /// <see cref="IHttpRequest.CancellationToken"/> is the only token
    /// validators see, so a short-circuit can cancel sibling pre-work
    /// without disturbing the request token. The local CTS is always
    /// cancelled in <c>finally</c> to prompt cooperative shutdown of any
    /// orphan pre-work, which is then awaited (with exceptions
    /// swallowed) so unobserved-task warnings never fire.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method,
        AllowMultiple = true, Inherited = true)]
    public class BoundParameterValidationHandlerAttribute
        : Attribute, IHandleMethodInvocation
    {
        public async Task<IHttpResponse> HandleMethodInvocationAsync(
            KeyValuePair<ParameterInfo, object>[] parameters,
            IReadOnlyDictionary<ParameterInfo, object> bindingContexts,
            MethodInfo method, IApplication httpApp, IHttpRequest request,
            InvokeMethodDelegate continueInvocation)
        {
            // ----- Gather IValidateHttpRequestForBoundParameters -----
            // Three sources, in registration (= run) order:
            //   1. method-level validators (owner = null)
            //   2. each bound parameter's type-level validators
            //      (owner = null)
            //   3. each bound parameter's own attributes
            //      (owner = the parameter)
            var entries = new List<(ParameterInfo owner,
                IValidateHttpRequestForBoundParameters validator)>();
            foreach (var v in method
                .GetAttributesInterface<IValidateHttpRequestForBoundParameters>(true, true))
                entries.Add((null, v));
            foreach (var binding in parameters)
            {
                if (binding.Key == null)
                    continue;
                foreach (var v in binding.Key.ParameterType
                    .GetAttributesInterface<IValidateHttpRequestForBoundParameters>(true, true))
                    entries.Add((null, v));
                foreach (var v in binding.Key
                    .GetAttributesInterface<IValidateHttpRequestForBoundParameters>(true))
                    entries.Add((binding.Key, v));
            }

            if (entries.Count == 0)
                return await continueInvocation(parameters, bindingContexts,
                    method, httpApp, request);

            using var localCts = CancellationTokenSource
                .CreateLinkedTokenSource(request.CancellationToken);

            var preWorkTasks = entries
                .Select(e => e.validator.ValidateBoundParametersForRequest(
                    e.owner, bindingContexts, parameters,
                    method, httpApp, request, localCts.Token))
                .ToArray();

            try
            {
                var mutations = await Task.WhenAll(preWorkTasks);

                // Bound-param mutation chain. Terminal hands off to the
                // outer IHandleMethodInvocation chain (continueInvocation).
                ContinueValidation chain = (reqFinal, parmsFinal) =>
                    continueInvocation(parmsFinal.ToArray(), bindingContexts,
                        method, httpApp, reqFinal);
                for (var i = mutations.Length - 1; i >= 0; i--)
                {
                    var mutation = mutations[i];
                    var capturedNext = chain;
                    chain = (reqCur, parmsCur) =>
                        mutation(reqCur, parmsCur, capturedNext);
                }

                return await chain(request, parameters);
            }
            finally
            {
                if (!localCts.IsCancellationRequested)
                    localCts.Cancel();
                // Observe orphans; ignore exceptions (a sibling already won).
                foreach (var task in preWorkTasks)
                {
                    try { await task.ConfigureAwait(false); } catch { /* swallowed */ }
                }
            }
        }
    }
}
