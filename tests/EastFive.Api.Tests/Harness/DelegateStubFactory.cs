using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using EastFive.Api;

namespace EastFive.Api.Tests.Harness;

/// <summary>
/// Synthesizes a stub instance for any delegate type whose Invoke method
/// returns <see cref="IHttpResponse"/>. The stub records (paramName, args)
/// into a <see cref="ResponseBranchCapture"/> that is bound at build time
/// (closed over) and returns a <see cref="StubHttpResponse"/>.
///
/// Binding the capture at build time — rather than reading
/// <see cref="CaptureScope.Current"/> at invoke time — matters: some
/// continuation paths drop <see cref="System.Threading.ExecutionContext"/>
/// flow, so an AsyncLocal read inside them would observe null and lose the
/// branch.
/// </summary>
internal static class DelegateStubFactory
{
    private static readonly ConcurrentDictionary<(Type DelegateType, string ParamName), Func<ResponseBranchCapture, Delegate>> cache = new();

    public static bool IsResponseDelegate(Type parameterType)
    {
        if (!typeof(Delegate).IsAssignableFrom(parameterType))
            return false;
        var invoke = parameterType.GetMethod("Invoke");
        if (invoke == null)
            return false;
        return typeof(IHttpResponse).IsAssignableFrom(invoke.ReturnType);
    }

    public static Delegate Build(Type delegateType, string parameterName, ResponseBranchCapture capture)
    {
        var factory = cache.GetOrAdd((delegateType, parameterName), key =>
        {
            var invoke = key.DelegateType.GetMethod("Invoke")!;
            var parameters = invoke.GetParameters();

            var captureParam = Expression.Parameter(typeof(ResponseBranchCapture), "capture");

            var paramExprs = parameters
                .Select(p => Expression.Parameter(p.ParameterType, p.Name))
                .ToArray();

            var argsArray = Expression.NewArrayInit(
                typeof(object),
                paramExprs.Select(p => Expression.Convert(p, typeof(object))));

            var body = Expression.Call(
                typeof(DelegateStubFactory).GetMethod(nameof(Record),
                    BindingFlags.NonPublic | BindingFlags.Static)!,
                captureParam,
                Expression.Constant(key.ParamName),
                argsArray);

            var innerLambda = Expression.Lambda(key.DelegateType, body, paramExprs);

            var outerLambda = Expression.Lambda<Func<ResponseBranchCapture, Delegate>>(
                Expression.Convert(innerLambda, typeof(Delegate)),
                captureParam);

            return outerLambda.Compile();
        });

        return factory(capture);
    }

    private static IHttpResponse Record(ResponseBranchCapture capture, string parameterName, object?[] arguments)
    {
        capture.Record(parameterName, arguments);
        return new StubHttpResponse();
    }
}
