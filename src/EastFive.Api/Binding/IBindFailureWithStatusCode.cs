using System.Net;

using EastFive.Serialization.Binding;

namespace EastFive.Api.Binding
{
    /// <summary>
    /// Optional bridge between a <see cref="IBindFailureReason"/> and the
    /// HTTP response the V3 dispatcher emits. Default behaviour for any
    /// <see cref="BindFailure"/> is a 400 with a diagnostic body; reasons
    /// that need a different status code (e.g. a storage loader's 404 for a
    /// missing row) implement this interface so
    /// <see cref="MethodDispatcherV3.BindAndInvokeAsync"/> can surface the
    /// correct status without bolting status codes onto the core
    /// <see cref="IBindFailureReason"/> shape.
    /// </summary>
    public interface IBindFailureWithStatusCode : IBindFailureReason
    {
        HttpStatusCode StatusCode { get; }
    }
}
