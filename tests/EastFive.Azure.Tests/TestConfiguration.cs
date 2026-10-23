using System.Security.Cryptography;
using System.Text;
using System.Threading;

using Microsoft.Extensions.Configuration;

namespace EastFive.Azure.Tests;

/// <summary>
/// Wires up the EastFive configuration subsystem from the test project's
/// appsettings.json exactly once per process (idempotent, thread-safe:
/// xUnit runs test classes in parallel).
///
/// Settings that are process-generated (the RSA token-signing key) or test-only
/// (a fake search endpoint) are published as in-process ENVIRONMENT VARIABLES
/// rather than an in-memory provider: the shared <c>TestApplication</c> harness
/// re-initializes configuration from appsettings.json + environment when it is
/// first built, so any value not visible through those two sources would be
/// dropped from under a test running concurrently.
/// </summary>
public static class TestConfiguration
{
    private static readonly object gate = new();
    private static bool initialized;

    /// <summary>Access-token lifetime pinned for tests (asserted by the expires_in test).</summary>
    public const int AccessTokenExpirationInMinutes = 17;

    public const string SigningKeyName = "EastFiveTokens-SigningKey";

    public const string SearchEndPoint = "https://fake-search.test/";
    public const string SearchAdminApiKey = "fake-admin-key";

    public static void Ensure()
    {
        if (Volatile.Read(ref initialized))
            return;

        lock (gate)
        {
            if (initialized)
                return;

            PublishProcessSettings();

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .AddEnvironmentVariables()
                .Build();

            EastFive.Web.Configuration.ConfigurationExtensions.Initialize(configuration);

            // Synchronous framework readers (the storage driver factory, token signing) never load:
            // a host loads its configuration once at startup. Test code may block here; framework
            // code never does.
            var issues = EastFive.Configuration.StartupConfiguration.LoadAsync(new[]
                {
                    typeof(EastFive.Azure.Persistence.StorageTablesConfiguration),
                    typeof(EastFive.Security.Tokens.TokenSigningConfiguration),
                    typeof(SearchConfiguration),
                })
                .GetAwaiter().GetResult();
            if (issues.Any())
                throw new InvalidOperationException($"Test configuration did not load: {string.Join("; ", issues.Select(issue => issue.ToString()))}");
            Volatile.Write(ref initialized, true);
        }
    }

    private static void PublishProcessSettings()
    {
        // JWT signing/validation: EastFive expects base64(RSA XML) — a fresh key per process,
        // so nothing key-shaped is committed.
        using var rsa = RSA.Create(2048);
        var rsaXmlBase64 = Convert.ToBase64String(Encoding.ASCII.GetBytes(rsa.ToXmlString(true)));
        // Secrets are never values: the key is a local secret override, read by its generated name.
        Set($"Secrets__{SigningKeyName}", rsaXmlBase64);
        Set(EastFive.Security.AppSettings.TokenIssuer, "https://tests.eastfive.invalid/");
        Set(EastFive.Security.AppSettings.TokenScope, "https://tests.eastfive.invalid/api");
        Set(EastFive.Security.AppSettings.TokenAlgorithm,
            Microsoft.IdentityModel.Tokens.SecurityAlgorithms.RsaSha256Signature);

        Set(EastFive.Azure.OAuth.Server.OAuthServer.AppSettings.AccessTokenExpirationInMinutes,
            AccessTokenExpirationInMinutes.ToString());

        Set(AppSettings.Search.EndPoint, SearchEndPoint);
        Set("Secrets__AzureSearch-AdminApiKey", SearchAdminApiKey);

        static void Set(string key, string value) =>
            Environment.SetEnvironmentVariable(key, value);
    }
}
