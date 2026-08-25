using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using EastFive.Api;

namespace EastFive.Api.Tests.Harness;

/// <summary>
/// Application-level <see cref="IHandleMethodInvocation"/> that intercepts
/// every controller-method invocation and replaces each response-delegate
/// parameter slot with a synthesized stub that records its invocation
/// to <see cref="CaptureScope.Current"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class CaptureResponseBranchesAttribute : Attribute, IHandleMethodInvocation
{
    public Task<IHttpResponse> HandleMethodInvocationAsync(
        KeyValuePair<ParameterInfo, object>[] parameters,
        IReadOnlyDictionary<ParameterInfo, object> bindingContexts,
        MethodInfo method, IApplication httpApp, IHttpRequest request,
        InvokeMethodDelegate continueInvocation)
    {
        var existingNames = new HashSet<string>(
            parameters
                .Where(kvp => kvp.Key != null && kvp.Key.Name != null)
                .Select(kvp => kvp.Key.Name!));

        var capture = CaptureScope.Current;
        if (capture == null)
            throw new InvalidOperationException(
                "CaptureResponseBranchesAttribute fired with no CaptureScope.Current set; " +
                "did the harness caller forget to seed one?");

        var augmented = new List<KeyValuePair<ParameterInfo, object>>(parameters);
        foreach (var p in method.GetParameters())
        {
            if (p.Name == null || existingNames.Contains(p.Name))
                continue;
            if (!DelegateStubFactory.IsResponseDelegate(p.ParameterType))
                continue;
            var stub = DelegateStubFactory.Build(p.ParameterType, p.Name, capture);
            augmented.Add(new KeyValuePair<ParameterInfo, object>(p, stub));
        }

        return continueInvocation(augmented.ToArray(), bindingContexts, method, httpApp, request);
    }
}
