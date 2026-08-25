using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;

namespace EastFive.Api.Binding
{
    /// <summary>
    /// V3 contract for resolving a method parameter from ambient request context
    /// rather than the request envelope. Examples: <see cref="IHttpRequest"/>,
    /// <see cref="IApplication"/>, <c>CancellationToken</c>, telemetry providers.
    /// Service resolution runs <b>after</b> method selection — service availability
    /// is a configuration concern, not a routing concern. A method parameter with
    /// no <see cref="IBindFromRequest"/> attribute and no registered service
    /// resolver produces a 500.
    /// </summary>
    public interface IProvideService
    {
        Type ServiceType { get; }

        ValueTask<object> ResolveAsync(IApplication app, IHttpRequest request,
            ParameterInfo parameter);
    }
}
