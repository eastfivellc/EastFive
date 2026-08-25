using System.Reflection;

namespace EastFive.Api
{
    /// <summary>
    /// Method-selection contract for the current (template-based) routing
    /// path. Implemented on each <c>HttpVerbAttribute</c> subclass.
    ///
    /// An attribute describes its route declaratively as a
    /// <see cref="RouteTemplate"/>: a compiled path regex + the set of HTTP
    /// verbs it accepts. The dispatcher matches the request line against the
    /// template and, for surviving candidates, drives
    /// <see cref="BindingRequirement"/>-based parameter matching against the
    /// envelope. Named regex captures flow into binding via
    /// <see cref="BindingSource.Path"/>.
    /// </summary>
    public interface IMatchRoute
    {
        /// <summary>
        /// Build a per-method route description.
        /// </summary>
        /// <param name="method">The candidate method this attribute decorates.</param>
        /// <param name="controller">The controller-level invoker
        /// (<c>FunctionViewControllerAttribute</c>) carrying namespace/route.</param>
        RouteTemplate GetRouteTemplate(MethodInfo method, IInvokeResource controller);
    }
}

