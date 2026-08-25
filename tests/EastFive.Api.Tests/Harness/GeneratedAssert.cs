using System;

namespace EastFive.Api.Tests.Harness;

/// <summary>
/// Branch assertions used by generated harness result structs. Each overload
/// checks that the controller invoked the expected response delegate (by name)
/// and, when a validator is supplied, hands it the strongly-typed arguments the
/// controller passed to that delegate.
/// </summary>
public static class GeneratedAssert
{
    public static void Branch(ResponseBranchCapture capture, string expectedBranch)
    {
        if (capture is null) throw new ArgumentNullException(nameof(capture));
        if (!string.Equals(capture.BranchName, expectedBranch, StringComparison.Ordinal))
            throw new HarnessAssertionException(
                $"response branch '{expectedBranch}'",
                $"'{capture.BranchName ?? "(no branch)"}'");
    }

    public static void Branch<T1>(
        ResponseBranchCapture capture, string expectedBranch, Action<T1>? validate)
    {
        Branch(capture, expectedBranch);
        if (validate is null)
            return;
        var args = Args(capture, expectedBranch, 1);
        validate(Cast<T1>(args, 0, expectedBranch));
    }

    public static void Branch<T1, T2>(
        ResponseBranchCapture capture, string expectedBranch, Action<T1, T2>? validate)
    {
        Branch(capture, expectedBranch);
        if (validate is null)
            return;
        var args = Args(capture, expectedBranch, 2);
        validate(Cast<T1>(args, 0, expectedBranch), Cast<T2>(args, 1, expectedBranch));
    }

    public static void Branch<T1, T2, T3>(
        ResponseBranchCapture capture, string expectedBranch, Action<T1, T2, T3>? validate)
    {
        Branch(capture, expectedBranch);
        if (validate is null)
            return;
        var args = Args(capture, expectedBranch, 3);
        validate(
            Cast<T1>(args, 0, expectedBranch),
            Cast<T2>(args, 1, expectedBranch),
            Cast<T3>(args, 2, expectedBranch));
    }

    private static object?[] Args(ResponseBranchCapture capture, string branch, int expectedCount)
    {
        var args = capture.Arguments ?? Array.Empty<object?>();
        if (args.Length < expectedCount)
            throw new HarnessAssertionException(
                $"at least {expectedCount} argument(s) on branch '{branch}'",
                $"{args.Length}");
        return args;
    }

    private static T Cast<T>(object?[] args, int index, string branch)
    {
        var value = args[index];
        if (value is null)
            return default!;
        if (value is T typed)
            return typed;
        throw new HarnessAssertionException(
            $"branch '{branch}' argument {index} of type '{typeof(T).FullName}'",
            $"'{value.GetType().FullName}'");
    }
}
