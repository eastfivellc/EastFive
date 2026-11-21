using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

using EastFive.Azure.Auth;
using EastFive.Azure.Persistence.AzureStorageTables;

namespace EastFive.Azure.Tests.Auth;

/// <summary>
/// Two writers on one <see cref="Authorization"/> row: a sign-out is held inside its mutation
/// while a token refresh saves new parameters. Neither change may be lost.
/// </summary>
public class AuthorizationConcurrentUpdateTests
{
    private const string RefreshTokenParameter = "refresh_token";
    private static readonly TimeSpan GateTimeout = TimeSpan.FromSeconds(30);

    public AuthorizationConcurrentUpdateTests() => TestConfiguration.Ensure();

    private static TaskCompletionSource NewGate() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public async Task SignOutHeldWhileARefreshSaves_KeepsBothChanges()
    {
        var authorizationRef = Guid.NewGuid().AsRef<Authorization>();
        var authorization = new Authorization
        {
            authorizationRef = authorizationRef,
            authorized = true,
            accountIdMaybe = Guid.NewGuid(),
            parameters = new Dictionary<string, string> { [RefreshTokenParameter] = "first" },
        };
        Assert.True(await authorization.StorageCreateAsync(created => true, onAlreadyExists: () => false));

        var inside = NewGate();
        var release = NewGate();
        var runs = 0;
        var signedOutOn = DateTime.UtcNow;

        var signOut = authorizationRef.StorageUpdateAsync<Authorization, bool>(
            async (toSignOut, save) =>
            {
                if (Interlocked.Increment(ref runs) == 1)
                {
                    inside.SetResult();
                    await release.Task;
                }
                toSignOut.deleted = signedOutOn;
                await save(toSignOut);
                return true;
            });

        try
        {
            await inside.Task.WaitAsync(GateTimeout);
            Assert.True(await authorizationRef.StorageUpdateAsync<Authorization, bool>(
                async (toRefresh, save) =>
                {
                    toRefresh.parameters = new Dictionary<string, string>(toRefresh.parameters)
                    {
                        [RefreshTokenParameter] = "refreshed",
                    };
                    await save(toRefresh);
                    return true;
                }));
        }
        finally
        {
            release.TrySetResult();
        }
        Assert.True(await signOut.WaitAsync(GateTimeout));

        var stored = await authorizationRef.StorageGetAsync(
            stored => stored,
            () => throw new InvalidOperationException("The authorization row is missing."));
        Assert.Equal(2, runs);
        Assert.True(stored.deleted.HasValue);
        Assert.Equal("refreshed", stored.parameters[RefreshTokenParameter]);
    }
}
