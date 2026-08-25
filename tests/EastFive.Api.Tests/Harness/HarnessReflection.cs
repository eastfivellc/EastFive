using System;
using System.Reflection;

namespace EastFive.Api.Tests.Harness;

/// <summary>
/// Reflection helpers used by generated harness extensions to resolve the
/// controller <see cref="MethodInfo"/> they dispatch. Resolution is by method
/// name plus the exact ordered parameter-name list, which uniquely identifies
/// a method even across overloads.
/// </summary>
public static class HarnessReflection
{
    private const BindingFlags Flags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    public static MethodInfo ResolveControllerMethod(
        Type controllerType, string methodName, string[] parameterNames)
    {
        if (controllerType is null) throw new ArgumentNullException(nameof(controllerType));
        if (methodName is null) throw new ArgumentNullException(nameof(methodName));
        parameterNames ??= Array.Empty<string>();

        foreach (var method in controllerType.GetMethods(Flags))
        {
            if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                continue;
            var parameters = method.GetParameters();
            if (parameters.Length != parameterNames.Length)
                continue;

            var match = true;
            for (var i = 0; i < parameters.Length; i++)
            {
                if (!string.Equals(parameters[i].Name, parameterNames[i], StringComparison.Ordinal))
                {
                    match = false;
                    break;
                }
            }

            if (match)
                return method;
        }

        throw new InvalidOperationException(
            $"Harness could not resolve {controllerType.FullName}.{methodName}(" +
            $"{string.Join(", ", parameterNames)}). Did the controller signature change " +
            "without regenerating the harness extensions?");
    }
}
