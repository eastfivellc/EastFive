using System.Threading;

using Microsoft.Extensions.Configuration;

namespace EastFive.Azure.Tests;

/// <summary>
/// Wires up the EastFive configuration subsystem from the test project's
/// appsettings.json exactly once per process (idempotent, thread-safe:
/// xUnit runs test classes in parallel).
/// </summary>
public static class TestConfiguration
{
    private static readonly object gate = new();
    private static bool initialized;

    public static void Ensure()
    {
        if (Volatile.Read(ref initialized))
            return;

        lock (gate)
        {
            if (initialized)
                return;

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .AddEnvironmentVariables()
                .Build();

            EastFive.Web.Configuration.ConfigurationExtensions.Initialize(configuration);
            Volatile.Write(ref initialized, true);
        }
    }
}
