using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace EastFive.Azure.Tests;

/// <summary>
/// This suite always targets local Azurite (appsettings.json pins
/// UseDevelopmentStorage=true). When Azurite is not listening, the Azure
/// Tables SDK retries with exponential backoff and the run HANGS rather
/// than fails; this guard aborts immediately with instructions instead.
/// </summary>
internal static class AzuriteGuard
{
    [ModuleInitializer]
    internal static void EnsureAzuriteIsListening()
    {
        using var client = new TcpClient();
        try
        {
            if (client.ConnectAsync("127.0.0.1", 10002).Wait(TimeSpan.FromSeconds(2)))
                return;
        }
        catch (AggregateException)
        {
            // connection refused — fall through to the failure below
        }

        throw new InvalidOperationException(
            "Azurite is not running (nothing listening on 127.0.0.1:10002), so storage-touching " +
            "tests would hang in Azure SDK retry backoff instead of failing. " +
            "Start Azurite (e.g. `npx azurite --silent` or the docker image " +
            "mcr.microsoft.com/azure-storage/azurite) and re-run.");
    }
}
