using System.Net;
using System.Text;

using Xunit;

using Azure;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Search.Documents;

using EastFive.Azure.Search;

namespace EastFive.Azure.Tests;

/// <summary>
/// FAILING ON PURPOSE — briefing for whoever fixes <c>SearchExtensions</c> indexing diagnostics.
///
/// THE DEFECT (src/EastFive.Azure/Search/SearchExtensions.cs, three identical copies: the two
/// <c>SearchUpdateBatchAsync</c> overloads and <c>SearchDeleteBatchAsync</c>):
/// <code>
///   catch (Exception)
///   {
///       // ... For now, just log the failed document keys and continue.
///       Console.WriteLine("Failed to index some of the documents: {0}");
///       throw;
///   }
/// </code>
/// 1. <c>{0}</c> is a LITERAL — <c>Console.WriteLine(string)</c> with no args. The keys are never
///    written; a consumer (Rosemary, publishing a bundle) sees exactly
///    "Failed to index some of the documents: {0}" and nothing else. That is what surfaced this.
/// 2. The comment promises "log the failed document keys and continue"; the code rethrows.
///    KEEP THE RETHROW — consumers depend on it (Rosemary's PatientWorkflowSearchIndex.UpsertAsync
///    wraps the call in try/catch and returns false; swallowing here would make it report success
///    on failure). Fix the comment, not the throw.
/// 3. <c>Console.WriteLine</c> bypasses any logging seam. EastFive.Azure has no ILogger convention
///    in this area (5 Console.WriteLine in the whole assembly); Console is acceptable, but the
///    message must carry the diagnosis.
///
/// SDK CONTEXT THAT CHANGES THE SHAPE OF THE FIX (Azure.Search.Documents 11.6.0):
/// - The comment's "failed document keys" language is from the legacy Microsoft.Azure.Search SDK
///   (IndexBatchException.FindFailedActionsToRetry). In the CURRENT SDK a partial failure does NOT
///   throw: IndexDocumentsAsync returns HTTP 207 and <c>IndexDocumentsResult.Results</c> carries
///   per-key <c>Succeeded</c>/<c>ErrorMessage</c>/<c>Status</c>. The catch block therefore never sees
///   per-document failures — only whole-batch failures (RequestFailedException: 4xx/5xx — auth,
///   index-not-found, throttling after the SDK's own retries).
/// - So there are TWO diagnostics to get right, and today NEITHER exists:
///   (a) whole-batch failure → the catch: report the exception message AND the keys that were in
///       the batch (they all failed), then rethrow;
///   (b) partial failure → the success path: inspect <c>result.Value.Results</c>, report the keys
///       with <c>Succeeded == false</c> and their ErrorMessage. Whether (b) should ALSO throw is
///       the one open contract decision — today it silently returns; the test below only requires
///       the keys be reported. Decide explicitly and say why in the commit.
/// - The document key: the batch is built from <c>IProvideSearchSerialization.GetSerializedObject</c>
///   (an ExpandoObject). The key field is the member carrying <c>[SearchKey]</c>; its serialized
///   name is <c>IProvideSearchField.GetKeyName(member)</c>. In the 207 path the SDK hands you the
///   key directly (<c>IndexingResult.Key</c>); in the catch path read it off the batch actions
///   (<c>IndexDocumentsAction&lt;T&gt;.Document</c> is the serialized ExpandoObject) — or resolve the
///   [SearchKey] member on <c>T</c> once and project the source items.
///
/// HOW THESE TESTS DRIVE THE CODE: the extension methods build their SearchClient from process
/// configuration, and the SDK refuses non-https endpoints, so a loopback listener cannot stand in.
/// Instead <c>SearchExtensions.ClientOptions.Transport</c> (added with this test — the one
/// production-code change made here, purely a seam) is swapped for a scripted transport that
/// answers every request with one status + body. Console output is captured for the assertion.
/// If your fix moves the diagnostic off Console, move the capture with it — the requirement is
/// that the failed keys are OBSERVABLE, not that Console is the channel.
///
/// Expected before the fix: <c>WholeBatchFailure_*</c> fails on the literal "{0}" / missing keys;
/// <c>PartialFailure_*</c> fails because nothing is written at all.
/// </summary>
public class SearchIndexingDiagnosticsTests
{
    [SearchIndex(Index = "diagnosticsprobe")]
    [SearchSerialization]
    public class ProbeDoc
    {
        [SearchKey]
        public string id = string.Empty;

        [SearchField]
        public string name = string.Empty;
    }

    [Fact]
    public async Task WholeBatchFailure_DiagnosticNamesTheKeysAndTheReason_ThenRethrows()
    {
        // 404 is non-retriable: the SDK throws on the first attempt instead of backing off.
        using var _ = ScriptService(
            HttpStatusCode.NotFound,
            """{"error":{"code":"","message":"The index 'diagnosticsprobe' for service 'fake' was not found."}}""");
        var docs = new[]
        {
            new ProbeDoc { id = "key-alpha", name = "a" },
            new ProbeDoc { id = "key-beta", name = "b" },
        };

        var (console, thrown) = await CaptureConsoleAsync(
            () => docs.SearchUpdateBatchAsync());

        Assert.IsAssignableFrom<RequestFailedException>(thrown);
        Assert.DoesNotContain("{0}", console);
        Assert.Contains("key-alpha", console);
        Assert.Contains("key-beta", console);
        Assert.Contains("was not found", console);
    }

    [Fact]
    public async Task PartialFailure_DiagnosticNamesOnlyTheFailedKeys()
    {
        // HTTP 207: the SDK does NOT throw; per-key status rides the result.
        using var _ = ScriptService(
            (HttpStatusCode)207,
            """
            {"value":[
              {"key":"key-ok","status":true,"errorMessage":null,"statusCode":200},
              {"key":"key-throttled","status":false,"errorMessage":"The service is under load.","statusCode":503}
            ]}
            """);
        var docs = new[]
        {
            new ProbeDoc { id = "key-ok", name = "a" },
            new ProbeDoc { id = "key-throttled", name = "b" },
        };

        var (console, thrown) = await CaptureConsoleAsync(
            () => docs.SearchUpdateBatchAsync());

        Assert.Null(thrown); // if the fix decides (b) should throw, change this deliberately
        Assert.Contains("key-throttled", console);
        Assert.Contains("under load", console);
        Assert.DoesNotContain("key-ok", console);
    }

    /// <summary>Point the static client factory at a fake service: an https-looking endpoint in
    /// configuration (the SDK validates the scheme, nothing else; published process-wide by
    /// <see cref="TestConfiguration"/>), scripted transport underneath.
    /// Serialized across tests because both the options and the console are process-global;
    /// disposing restores the default options for the rest of the suite.</summary>
    private static IDisposable ScriptService(HttpStatusCode status, string body)
    {
        TestConfiguration.Ensure();

        var previous = SearchExtensions.ClientOptions;
        var options = new SearchClientOptions
        {
            Transport = new ScriptedTransport(status, body),
        };
        options.Retry.MaxRetries = 0;
        SearchExtensions.ClientOptions = options;
        return new Restore(() => SearchExtensions.ClientOptions = previous);
    }

    private sealed class Restore(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }

    private sealed class ScriptedTransport(HttpStatusCode status, string body) : HttpPipelineTransport
    {
        public override Request CreateRequest() => new HttpClientTransport().CreateRequest();

        public override void Process(HttpMessage message) =>
            message.Response = new ScriptedResponse((int)status, body, message.Request.ClientRequestId);

        public override ValueTask ProcessAsync(HttpMessage message)
        {
            Process(message);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ScriptedResponse : Response
    {
        private readonly Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Content-Type"] = "application/json",
        };

        public ScriptedResponse(int status, string body, string clientRequestId)
        {
            Status = status;
            ClientRequestId = clientRequestId;
            ContentStream = new MemoryStream(Encoding.UTF8.GetBytes(body));
        }

        public override int Status { get; }
        public override string ReasonPhrase => string.Empty;
        public override Stream? ContentStream { get; set; }
        public override string ClientRequestId { get; set; }
        public override void Dispose() => ContentStream?.Dispose();

        protected override bool TryGetHeader(string name, out string? value) =>
            headers.TryGetValue(name, out value);
        protected override bool TryGetHeaderValues(string name, out IEnumerable<string>? values)
        {
            var found = headers.TryGetValue(name, out var value);
            values = found ? [value!] : null;
            return found;
        }
        protected override bool ContainsHeader(string name) => headers.ContainsKey(name);
        protected override IEnumerable<HttpHeader> EnumerateHeaders() =>
            headers.Select(kvp => new HttpHeader(kvp.Key, kvp.Value));
    }

    private static readonly SemaphoreSlim globalsGate = new(1, 1);

    private static async Task<(string console, Exception? thrown)> CaptureConsoleAsync(
        Func<Task> act)
    {
        await globalsGate.WaitAsync();
        var original = Console.Out;
        var writer = new StringWriter();
        try
        {
            Console.SetOut(writer);
            try
            {
                await act();
                return (writer.ToString(), null);
            }
            catch (Exception ex)
            {
                return (writer.ToString(), ex);
            }
        }
        finally
        {
            Console.SetOut(original);
            globalsGate.Release();
        }
    }
}
