using System;
using System.Collections.Generic;
using System.Threading;

using EastFive.Api;

namespace EastFive.Api.Tests.Harness;

/// <summary>
/// Mutable per-invocation bag that records which response delegate fired
/// and with what arguments. First-write-wins: subsequent calls (rare —
/// would indicate a controller bug) are ignored. Population happens via
/// <see cref="CaptureResponseBranchesAttribute"/>'s synthesized stubs.
/// </summary>
public sealed class ResponseBranchCapture
{
    private int recorded;

    public string? BranchName { get; private set; }
    public object?[]? Arguments { get; private set; }

    /// <summary>The response the pipeline ultimately returned. When a method-level
    /// gate (auth attribute) short-circuits, no branch fires and this is the only
    /// evidence of the outcome (status code, WWW-Authenticate, X-Reason).</summary>
    public IHttpResponse? Response { get; set; }

    public void Record(string branchName, object?[] arguments)
    {
        if (Interlocked.CompareExchange(ref recorded, 1, 0) != 0)
            return;
        this.BranchName = branchName;
        this.Arguments = arguments;
    }

    public bool TryGet(string branchName, out object?[] arguments)
    {
        if (BranchName == branchName && Arguments != null)
        {
            arguments = Arguments;
            return true;
        }
        arguments = Array.Empty<object?>();
        return false;
    }
}

/// <summary>
/// AsyncLocal sink so harness callers can stash a fresh
/// <see cref="ResponseBranchCapture"/> before invoking the dispatch
/// pipeline; the synthesized stubs read it from any thread inside the
/// async chain.
/// </summary>
public static class CaptureScope
{
    private static readonly AsyncLocal<ResponseBranchCapture?> current = new();

    public static ResponseBranchCapture? Current
    {
        get => current.Value;
        set => current.Value = value;
    }
}
