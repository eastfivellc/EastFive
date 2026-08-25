using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace EastFive.Api
{
    /// <summary>
    /// Continuation handed to a <see cref="ParameterMutation"/>. Invoking it
    /// runs the next validator's mutation closure, then the next, and so on
    /// down the chain — terminating in the outer
    /// <see cref="IHandleMethodInvocation"/> chain (which ultimately invokes
    /// the controller). The <see cref="IHttpRequest"/> and parameter list
    /// flow through every link, so a mutation's effects (parameter slot
    /// replaced, request enriched) are visible downstream. To short-circuit,
    /// the mutation simply does NOT invoke the continuation and returns its
    /// own response instead.
    /// </summary>
    public delegate Task<IHttpResponse> ContinueValidation(
        IHttpRequest request,
        IReadOnlyList<KeyValuePair<ParameterInfo, object>> parameters);

    /// <summary>
    /// Closure produced by a parameter-bound validator's async pre-work.
    /// Receives the current request and parameter list and the chain
    /// continuation. Either invokes <paramref name="continueValidation"/>
    /// (optionally with a fresh request/parameter list reflecting its
    /// mutation) or returns a short-circuit response without calling it.
    /// </summary>
    /// <remarks>
    /// Mutations run sequentially in registration order; by the time mutation
    /// k runs, mutations 0..k-1 have already applied so closure k can read
    /// their effects via the live <c>request</c> and <c>parameters</c>
    /// arguments. The list passed to <c>continueValidation</c> is what the
    /// next link will see — replace a slot value to publish a mutation.
    /// </remarks>
    public delegate Task<IHttpResponse> ParameterMutation(
        IHttpRequest request,
        IReadOnlyList<KeyValuePair<ParameterInfo, object>> parameters,
        ContinueValidation continueValidation);

    /// <summary>
    /// Parameter-bound validator that participates in the parallel pre-work
    /// + chained mutation pipeline composed by
    /// <c>MethodDispatcher.BindAndInvokeAsync</c>. Used by
    /// loader attributes that need to perform async I/O (storage loads,
    /// downstream lookups) keyed off bound parameter values.
    ///
    /// Pre-work tasks for ALL registered parameter-bound validators are
    /// launched in parallel before any mutation runs. Each validator's
    /// returned <see cref="ParameterMutation"/> is invoked sequentially in
    /// registration order — chain order is the only short-circuit rule.
    /// There is no discriminated outcome type and no "first wins" tie-break.
    ///
    /// Inter-validator dependencies are forbidden by contract: if validator B
    /// needs validator A's output, the two must be combined into a single
    /// validator. The framework provides no <c>Phase</c>, <c>DependsOn</c>,
    /// or sequential-tier mechanism.
    /// </summary>
    public interface IValidateHttpRequestForBoundParameters
    {
        /// <summary>
        /// Async pre-work for this validator. Runs in parallel with all
        /// sibling validators. The returned <see cref="ParameterMutation"/>
        /// will be invoked sequentially after siblings registered earlier
        /// have completed both their pre-work and their mutation step.
        /// </summary>
        /// <param name="owner">
        /// The parameter this validator is attached to.
        /// </param>
        /// <param name="bindingContexts">
        /// Per-parameter context produced by
        /// <c>IProvideBindingRequirements.GetBindingContext</c> (e.g. a
        /// loader's wire-level (name, value) bindings for diagnostic 404
        /// messages). Keys are the owning parameters; entries may be absent
        /// for parameters that did not produce context. All validators see
        /// all contexts.
        /// </param>
        /// <param name="parameterSelection">
        /// Read-only snapshot of the slot list as it stood at bind
        /// completion. Mutations applied by sibling validators are NOT
        /// visible here — the closure receives the live, mutated list when
        /// it runs.
        /// </param>
        /// <param name="cancellationToken">
        /// Local validation-batch token, linked to
        /// <c>IHttpRequest.CancellationToken</c>. Cancellation indicates a
        /// sibling has short-circuited and the orchestrator is unwinding;
        /// long-running pre-work should observe it cooperatively.
        /// </param>
        Task<ParameterMutation> ValidateBoundParametersForRequest(
            ParameterInfo owner,
            IReadOnlyDictionary<ParameterInfo, object> bindingContexts,
            IReadOnlyList<KeyValuePair<ParameterInfo, object>> parameterSelection,
            MethodInfo method,
            IApplication httpApp,
            IHttpRequest routeData,
            CancellationToken cancellationToken);
    }
}
