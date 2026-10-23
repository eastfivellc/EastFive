using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Xunit;

using EastFive.Azure.Persistence;
using EastFive.Configuration;
using EastFive.Persistence.Azure.StorageTables.Driver;

namespace EastFive.Azure.Tests;

/// <summary>
/// Account-named storage configuration over the secret store: the account comes from the values
/// store, the credential from the secret store by its generated name
/// (<c>AzureStorage-{account}-ConnectionString</c>), emulator fabrication (and the things that
/// must NEVER fabricate), the account/credential cross-checks, and the driver routing.
/// </summary>
public class StorageAccountConfigurationTests
{
    private const string AccountKey = "EastFive.Azure.StorageTables.Account";
    private const string EmulatorKey = "Azure.Storage.Emulator.ConnectionString";
    private const string PortKey = "Azure.Storage.Emulator.Port";
    private const string DevName = "AzureStorage-rosemarydev-ConnectionString";
    private const string DevString = "DefaultEndpointsProtocol=https;AccountName=rosemarydev;AccountKey=ZGV2;EndpointSuffix=core.windows.net";
    private const string OtherString = "DefaultEndpointsProtocol=https;AccountName=otheracct;AccountKey=bGVn;EndpointSuffix=core.windows.net";

    /// <summary>Values and secrets, recorded separately so a test can prove which store was asked.</summary>
    private sealed class Sources : ISecretStore
    {
        public readonly List<string> ValueReads = new();
        public readonly List<string> SecretReads = new();
        public readonly Dictionary<string, string> Values = new();
        public readonly Dictionary<string, string> Secrets = new();
        public Func<string, Exception?> FailValue = _ => null;
        public bool SecretStoreDown;

        public string? Read(string key)
        {
            ValueReads.Add(key);
            if (FailValue(key) is { } error)
                throw error;
            return Values.TryGetValue(key, out var value) ? value : null;
        }

        public IReadOnlyList<SecretSource> Sources_ => new[] { new SecretSource(SecretSource.Vault, "test.vault") };
        IReadOnlyList<SecretSource> ISecretStore.Sources => Sources_;

        public Task<TResult> GetSecretAsync<TResult>(string name, Func<Secret, SecretSource, TResult> onFound,
            Func<TResult> onMissing, Func<string, TResult> onFailure)
        {
            SecretReads.Add(name);
            return Task.FromResult(SecretStoreDown ? onFailure("Vault test.vault answered 503.")
                : Secrets.TryGetValue(name, out var value) ? onFound(new Secret(value), Sources_[0]) : onMissing());
        }
    }

    private static Sources With(params (string key, string value)[] values)
    {
        var sources = new Sources();
        foreach (var (key, value) in values)
            sources.Values[key] = value;
        return sources;
    }

    private static Task<string> Outcome(Sources sources, Action<StorageTablesConfiguration>? inspect = null)
        => ConfigurationLoader.LoadAsync<StorageTablesConfiguration, string>(sources.Read, sources,
            config => { inspect?.Invoke(config); return "configured"; },
            issue => $"missing:{issue.Key}",
            issue => $"invalid:{issue.Key}",
            issue => $"source-failure:{issue.Key}");

    [Fact]
    public void Declaration_is_the_single_source_of_keys_and_names()
    {
        Assert.Equal(AccountKey, StorageAccountConfiguration.AccountKey<StorageTablesConfiguration>());
        Assert.Equal(new[] { AccountKey }, ConfigurationMembers.Keys<StorageTablesConfiguration>());
        Assert.Equal(new[] { "EastFive.Azure.Spa.Account" }, ConfigurationMembers.Keys<SpaStorageConfiguration>());
        Assert.Equal(new[] { "EastFive.Azure.DataLake.Account" }, ConfigurationMembers.Keys<DataLakeStorageConfiguration>());
        Assert.Equal(DevName, StorageAccountConfiguration.SecretName("rosemarydev"));
        Assert.Equal(DevName, new StorageTablesConfiguration { Account = "rosemarydev" }.ConnectionStringName);
        // Stores on one account share one secret.
        Assert.Equal(DevName, new DataLakeStorageConfiguration { Account = "rosemarydev" }.ConnectionStringName);
    }

    [Fact]
    public async Task Account_names_the_secret_which_comes_only_from_the_secret_store()
    {
        var sources = With((AccountKey, "rosemarydev"), (DevName, OtherString), ("EastFive.Azure.StorageTables.ConnectionString", OtherString));
        sources.Secrets[DevName] = DevString;
        Assert.Equal("configured", await Outcome(sources, config => Assert.Equal(DevString, config.ConnectionString.Reveal())));
        Assert.Equal(new[] { DevName }, sources.SecretReads);
        Assert.Equal(new[] { AccountKey }, sources.ValueReads);
    }

    [Fact]
    public async Task Blank_account_is_missing_there_is_no_legacy_connection_string()
    {
        var sources = With(("EastFive.Azure.StorageTables.ConnectionString", DevString));
        Assert.Equal($"missing:{AccountKey}", await Outcome(sources));
        Assert.Empty(sources.SecretReads);
    }

    [Fact]
    public async Task Named_account_missing_from_the_store_is_the_callers_decision_and_can_be_supplied()
    {
        var sources = With((AccountKey, "rosemarydev"));
        Assert.Equal($"missing:{DevName}", await Outcome(sources));
        var supplied = await ConfigurationLoader.LoadAsync(() => new StorageTablesConfiguration(), sources.Read, sources,
            config => config.ConnectionString.Reveal(),
            (issue, supply) => supply(DevString),
            issue => "invalid", issue => "source-failure");
        Assert.Equal(DevString, supplied);
    }

    [Fact]
    public async Task Credential_for_a_different_account_than_named_is_invalid()
    {
        var sources = With((AccountKey, "rosemarydev"));
        sources.Secrets[DevName] = OtherString;
        Assert.Equal($"invalid:{DevName}", await Outcome(sources));
    }

    [Fact]
    public async Task Emulator_account_fabricates_azurite_and_never_touches_the_secret_store()
    {
        var sources = With((AccountKey, "devstoreaccount1"));
        sources.Secrets["AzureStorage-devstoreaccount1-ConnectionString"] = DevString;
        Assert.Equal("configured", await Outcome(sources, config =>
        {
            var revealed = config.ConnectionString.Reveal();
            Assert.Contains("AccountName=devstoreaccount1;", revealed);
            Assert.Contains("BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1", revealed);
            Assert.Contains("QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1", revealed);
            Assert.Contains("TableEndpoint=http://127.0.0.1:10002/devstoreaccount1", revealed);
        }));
        Assert.Empty(sources.SecretReads);
    }

    [Fact]
    public async Task Emulator_port_moves_all_three_endpoints()
    {
        var sources = With((AccountKey, "devstoreaccount1"), (PortKey, "11000"));
        Assert.Equal("configured", await Outcome(sources, config =>
        {
            Assert.Contains(":11000/", config.ConnectionString.Reveal());
            Assert.Contains(":11001/", config.ConnectionString.Reveal());
            Assert.Contains(":11002/", config.ConnectionString.Reveal());
        }));
    }

    [Theory]
    [InlineData("eleven-thousand")]
    [InlineData("0")]
    [InlineData("65535")]
    public async Task Unusable_emulator_port_is_invalid_not_fabricated(string port)
        => Assert.Equal($"invalid:{PortKey}", await Outcome(With((AccountKey, "devstoreaccount1"), (PortKey, port))));

    [Fact]
    public async Task Explicit_emulator_string_wins_over_fabrication_when_it_stays_on_loopback()
    {
        var custom = StorageAccountConfiguration.EmulatorConnectionString(12000).Replace("127.0.0.1", "localhost");
        var sources = With((AccountKey, "devstoreaccount1"), (EmulatorKey, custom), (PortKey, "10000"));
        Assert.Equal("configured", await Outcome(sources, config => Assert.Equal(custom, config.ConnectionString.Reveal())));
    }

    [Fact]
    public async Task Emulator_account_pointed_off_loopback_is_invalid()
    {
        var remote = StorageAccountConfiguration.EmulatorConnectionString().Replace("127.0.0.1", "storage.example.net");
        var name = "AzureStorage-devstoreaccount1-ConnectionString";
        Assert.Equal($"invalid:{name}", await Outcome(With((AccountKey, "devstoreaccount1"), (EmulatorKey, remote))));
        Assert.Equal($"invalid:{name}", await Outcome(With((AccountKey, "devstoreaccount1"), (EmulatorKey, DevString))));
    }

    [Fact]
    public async Task Source_failure_is_never_papered_over_with_the_emulator()
    {
        var storeDown = With((AccountKey, "rosemarydev"));
        storeDown.SecretStoreDown = true;
        Assert.Equal($"source-failure:{DevName}", await Outcome(storeDown));

        var portUnreadable = With((AccountKey, "devstoreaccount1"));
        portUnreadable.FailValue = key => key == PortKey ? new TimeoutException("config") : null;
        Assert.Equal($"source-failure:{PortKey}", await Outcome(portUnreadable));
    }

    [Fact]
    public async Task Inspection_names_the_generated_secret_and_where_it_came_from()
    {
        var emulator = await ConfigurationLoader.InspectAsync<StorageTablesConfiguration>(With((AccountKey, "devstoreaccount1")).Read, new Sources());
        Assert.Equal(StorageAccountConfiguration.EmulatorSource, emulator.Single(row => row.Member.IsSecret).Source);

        var sources = With((AccountKey, "rosemarydev"));
        Assert.Equal(new[] { DevName }, await ConfigurationLoader.MissingAsync<StorageTablesConfiguration>(sources.Read, sources));
        sources.Secrets[DevName] = DevString;
        var rows = await ConfigurationLoader.InspectAsync<StorageTablesConfiguration>(sources.Read, sources);
        Assert.Equal(new[] { AccountKey, DevName }, rows.Select(row => row.Name));
        Assert.Equal("vault (test.vault)", rows[1].Source);
        Assert.Equal(new[] { AccountKey }, await ConfigurationLoader.MissingAsync<StorageTablesConfiguration>(With().Read, sources));
    }

    [Fact]
    public async Task Issues_and_configuration_never_leak_the_credential()
    {
        var issue = new ConfigIssue(typeof(StorageTablesConfiguration), DevName, ConfigurationMemberKind.Secret, ConfigIssueCategory.Invalid);
        Assert.DoesNotContain("ZGV2", issue.ToString());
        var sources = With((AccountKey, "rosemarydev"));
        sources.Secrets[DevName] = DevString;
        Assert.Equal("configured", await Outcome(sources, config =>
        {
            Assert.DoesNotContain("ZGV2", Newtonsoft.Json.JsonConvert.SerializeObject(config));
            Assert.DoesNotContain("ZGV2", config.ConnectionString.ToString());
        }));
    }

    [Fact]
    public async Task Driver_loads_from_the_fabricated_emulator_string()
    {
        var sources = With((AccountKey, "devstoreaccount1"), (PortKey, "11000"));
        var driver = await ConfigurationLoader.LoadAsync<StorageTablesConfiguration, AzureTableDriverDynamic>(sources.Read, sources,
            config => config.LoadDriver(),
            issue => throw new Exception(issue.ToString()),
            issue => throw new Exception(issue.ToString()),
            issue => throw new Exception(issue.ToString()));
        Assert.Equal(11002, driver.TableClient.BaseUri.Port);
        Assert.Equal(11000, driver.BlobClient.Uri.Port);
    }

    [Fact]
    public void Catalog_lists_every_framework_store_with_its_shared_generated_name()
    {
        var catalog = ConfigurationCatalog.For(new[] { typeof(StorageAccountConfiguration).Assembly });
        var stores = catalog.Where(type => typeof(StorageAccountConfiguration).IsAssignableFrom(type.Type)).ToArray();
        Assert.Equal(new[] { nameof(DataLakeStorageConfiguration), nameof(SpaStorageConfiguration), nameof(StorageTablesConfiguration) },
            stores.Select(type => type.Type.Name).OrderBy(name => name));
        Assert.All(stores, store => Assert.Equal(new[] { "AzureStorage-{account}-ConnectionString" }, store.SecretNames()));
    }

    [Fact]
    public void FromSettings_serves_the_store_the_host_loaded_at_startup()
    {
        TestConfiguration.Ensure();
        var driver = AzureTableDriverDynamic.FromSettings();
        Assert.Equal(10002, driver.TableClient.BaseUri.Port);
        Assert.Equal(10002, AzureTableDriverDynamic.FromSettings<StorageTablesConfiguration>().TableClient.BaseUri.Port);
    }

    [Fact]
    public void StorageAccount_handles_route_every_reader_through_the_typed_store()
    {
        TestConfiguration.Ensure();
        Assert.Contains("AccountName=devstoreaccount1;", StorageAccount.Tables.ConnectionStringOrThrow());
        Assert.Equal("devstoreaccount1", StorageAccount.Tables.AccountName);
        Assert.Equal(StorageAccount.Tables, StorageAccount.Of<StorageTablesConfiguration>());

        // An abfs uri belongs to the data lake only when its account is the lake's.
        var uri = new EastFive.Azure.Persistence.StorageTables.AzureBlobFileSystemUri("container", "a/b.parquet", "lakeacct");
        StartupConfiguration.Forget<DataLakeStorageConfiguration>();
        Assert.Equal(StorageAccount.Tables, uri.StorageAccount);
        StartupConfiguration.Set(new DataLakeStorageConfiguration { Account = "lakeacct", ConnectionString = new Secret(DevString) });
        try
        {
            Assert.Equal(StorageAccount.DataLake, uri.StorageAccount);
            Assert.Equal("lakeacct", EastFive.Azure.Persistence.StorageTables.AzureBlobFileSystemUri
                .FromStorageAccount(StorageAccount.DataLake, "c", "p").storageName);
        }
        finally
        {
            StartupConfiguration.Forget<DataLakeStorageConfiguration>();
        }
    }

    [Fact]
    public void FromSettings_for_a_store_the_host_never_loaded_names_it_rather_than_loading()
    {
        StartupConfiguration.Forget<SpaStorageConfiguration>();
        var error = Assert.Throws<EastFive.Web.ConfigurationException>(() => AzureTableDriverDynamic.FromSettings<SpaStorageConfiguration>());
        Assert.Contains(nameof(SpaStorageConfiguration), error.Message);
        Assert.Contains(nameof(ConfigIssueCategory.NotLoaded), error.Message);
    }
}
