using System;
using System.Collections.Generic;
using System.Linq;

using Xunit;

using EastFive.Azure.Persistence;
using EastFive.Configuration;
using EastFive.Persistence.Azure.StorageTables.Driver;

namespace EastFive.Azure.Tests;

/// <summary>
/// Account-named storage configuration: which key the credential is read from for a given
/// account name, emulator fabrication (and the things that must NEVER fabricate), the
/// account/credential cross-checks, and the driver routing. In-memory sources only.
/// </summary>
public class StorageAccountConfigurationTests
{
    private const string TablesKey = "EastFive.Azure.StorageTables.ConnectionString";
    private const string AccountKey = "EastFive.Azure.StorageTables.Account";
    private const string EmulatorKey = "Azure.Storage.Emulator.ConnectionString";
    private const string PortKey = "Azure.Storage.Emulator.Port";
    private const string DevVaultKey = "Azure.Storage.Account.rosemarydev";
    private const string DevString = "DefaultEndpointsProtocol=https;AccountName=rosemarydev;AccountKey=ZGV2;EndpointSuffix=core.windows.net";
    private const string LegacyString = "DefaultEndpointsProtocol=https;AccountName=legacyacct;AccountKey=bGVn;EndpointSuffix=core.windows.net";

    private sealed class Source
    {
        public readonly List<string> Reads = new();
        private readonly IReadOnlyDictionary<string, string> values;
        public Source(IReadOnlyDictionary<string, string> values) { this.values = values; }
        public string? Read(string key) { Reads.Add(key); return values.TryGetValue(key, out var value) ? value : null; }
    }

    private static Source With(params (string key, string value)[] values)
        => new(values.ToDictionary(pair => pair.key, pair => pair.value));

    private static string Outcome(Source source, Action<StorageTablesConfiguration>? inspect = null)
        => StorageAccountConfiguration.Load<StorageTablesConfiguration, string>(source.Read,
            config => { inspect?.Invoke(config); return "configured"; },
            (issue, _) => $"missing:{issue.Key}",
            issue => $"invalid:{issue.Key}",
            issue => $"source-failure:{issue.Key}");

    [Fact]
    public void Declaration_is_the_single_source_of_keys()
    {
        Assert.Equal(AccountKey, StorageAccountConfiguration.AccountKey<StorageTablesConfiguration>());
        Assert.Equal(TablesKey, new StorageTablesConfiguration().ConnectionStringKey);
        Assert.Equal(new[] { AccountKey, TablesKey }, ConfigurationMembers.Keys<StorageTablesConfiguration>().Where(key => key != PortKey));
        Assert.Contains(PortKey, ConfigurationMembers.PublicKeys<StorageTablesConfiguration>());
        Assert.Equal(new[] { TablesKey }, ConfigurationMembers.SecretKeys<StorageTablesConfiguration>());
        Assert.Equal(new[] { "EastFive.Azure.Spa.Account", "EastFive.Azure.Spa.ConnectionString" },
            ConfigurationMembers.Keys<SpaStorageConfiguration>().Where(key => key != PortKey));
        Assert.Equal(new[] { "EastFive.Azure.DataLake.Account", "EastFive.Azure.DataLake.ConnectionString" },
            ConfigurationMembers.Keys<DataLakeStorageConfiguration>().Where(key => key != PortKey));
    }

    [Fact]
    public void Blank_account_reads_the_legacy_connection_string()
    {
        var source = With((TablesKey, LegacyString), (DevVaultKey, DevString));
        Assert.Equal("configured", Outcome(source, config =>
        {
            Assert.Null(config.Account);
            Assert.Equal(TablesKey, config.ResolvedConnectionStringKey);
            Assert.Equal(LegacyString, config.ConnectionString.Reveal());
        }));
        Assert.DoesNotContain(DevVaultKey, source.Reads);
    }

    [Fact]
    public void Named_account_is_authoritative_over_the_legacy_string()
    {
        var source = With((AccountKey, "RosemaryDev"), (TablesKey, LegacyString), (DevVaultKey, DevString));
        Assert.Equal("configured", Outcome(source, config =>
        {
            Assert.Equal(DevVaultKey, config.ResolvedConnectionStringKey);
            Assert.Equal(DevString, config.ConnectionString.Reveal());
        }));
        Assert.DoesNotContain(TablesKey, source.Reads);
    }

    [Fact]
    public void Named_account_missing_from_the_vault_is_the_callers_decision_and_can_be_supplied()
    {
        var source = With((AccountKey, "rosemarydev"), (TablesKey, LegacyString));
        Assert.Equal($"missing:{DevVaultKey}", Outcome(source));
        var supplied = StorageAccountConfiguration.Load<StorageTablesConfiguration, string>(source.Read,
            config => config.ConnectionString.Reveal(),
            (issue, supply) => supply(DevString),
            issue => "invalid", issue => "source-failure");
        Assert.Equal(DevString, supplied);
    }

    [Fact]
    public void Credential_for_a_different_account_than_named_is_invalid()
    {
        var source = With((AccountKey, "rosemarydev"), (DevVaultKey, LegacyString));
        Assert.Equal($"invalid:{TablesKey}", Outcome(source));
    }

    [Fact]
    public void Emulator_account_fabricates_azurite_and_never_touches_the_vault()
    {
        var source = With((AccountKey, "devstoreaccount1"), (TablesKey, LegacyString),
            ("Azure.Storage.Account.devstoreaccount1", DevString));
        Assert.Equal("configured", Outcome(source, config =>
        {
            Assert.Equal(EmulatorKey, config.ResolvedConnectionStringKey);
            Assert.Equal(10000, config.EmulatorPort);
            var revealed = config.ConnectionString.Reveal();
            Assert.Contains("AccountName=devstoreaccount1;", revealed);
            Assert.Contains("BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1", revealed);
            Assert.Contains("QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1", revealed);
            Assert.Contains("TableEndpoint=http://127.0.0.1:10002/devstoreaccount1", revealed);
        }));
        Assert.DoesNotContain("Azure.Storage.Account.devstoreaccount1", source.Reads);
        Assert.DoesNotContain(TablesKey, source.Reads);
    }

    [Fact]
    public void Emulator_port_moves_all_three_endpoints()
    {
        var source = With((AccountKey, "devstoreaccount1"), (PortKey, "11000"));
        Assert.Equal("configured", Outcome(source, config =>
        {
            Assert.Equal(11000, config.EmulatorPort);
            Assert.Contains(":11000/", config.ConnectionString.Reveal());
            Assert.Contains(":11001/", config.ConnectionString.Reveal());
            Assert.Contains(":11002/", config.ConnectionString.Reveal());
        }));
    }

    [Theory]
    [InlineData("eleven-thousand")]
    [InlineData("0")]
    [InlineData("65535")]
    public void Unusable_emulator_port_is_invalid_not_fabricated(string port)
    {
        var source = With((AccountKey, "devstoreaccount1"), (PortKey, port));
        Assert.Equal($"invalid:{PortKey}", Outcome(source));
    }

    [Fact]
    public void Explicit_emulator_string_wins_over_fabrication_when_it_stays_on_loopback()
    {
        var custom = StorageAccountConfiguration.EmulatorConnectionString(12000).Replace("127.0.0.1", "localhost");
        var source = With((AccountKey, "devstoreaccount1"), (EmulatorKey, custom), (PortKey, "10000"));
        Assert.Equal("configured", Outcome(source, config => Assert.Equal(custom, config.ConnectionString.Reveal())));
    }

    [Fact]
    public void Emulator_account_pointed_off_loopback_is_invalid()
    {
        var remote = StorageAccountConfiguration.EmulatorConnectionString().Replace("127.0.0.1", "storage.example.net");
        Assert.Equal($"invalid:{TablesKey}", Outcome(With((AccountKey, "devstoreaccount1"), (EmulatorKey, remote))));
        Assert.Equal($"invalid:{TablesKey}", Outcome(With((AccountKey, "devstoreaccount1"), (EmulatorKey, DevString))));
    }

    [Fact]
    public void Source_failure_is_never_papered_over_with_the_emulator()
    {
        var accountSource = With((AccountKey, "rosemarydev"));
        var vaultDown = StorageAccountConfiguration.Load<StorageTablesConfiguration, string>(
            key => key == DevVaultKey ? throw new TimeoutException("vault") : accountSource.Read(key),
            _ => "configured", (issue, _) => "missing", _ => "invalid", issue => $"source-failure:{issue.Key}");
        Assert.Equal($"source-failure:{DevVaultKey}", vaultDown);

        var emulatorSource = With((AccountKey, "devstoreaccount1"));
        var portUnreadable = StorageAccountConfiguration.Load<StorageTablesConfiguration, string>(
            key => key == PortKey ? throw new TimeoutException("config") : emulatorSource.Read(key),
            _ => "configured", (issue, _) => "missing", _ => "invalid", issue => $"source-failure:{issue.Key}");
        Assert.Equal($"source-failure:{PortKey}", portUnreadable);
    }

    [Fact]
    public void Checklist_omits_the_fabricated_emulator_key_but_lists_a_missing_vault_secret()
    {
        Assert.Empty(StorageAccountConfiguration.Missing<StorageTablesConfiguration>(With((AccountKey, "devstoreaccount1")).Read));
        Assert.Equal(new[] { DevVaultKey }, StorageAccountConfiguration.Missing<StorageTablesConfiguration>(With((AccountKey, "rosemarydev")).Read));
        Assert.Equal(new[] { TablesKey }, StorageAccountConfiguration.Missing<StorageTablesConfiguration>(With().Read));
    }

    [Fact]
    public void Issues_and_configuration_never_leak_the_credential()
    {
        var issue = new ConfigIssue(typeof(StorageTablesConfiguration), DevVaultKey, ConfigurationMemberKind.Secret, ConfigIssueCategory.Invalid);
        Assert.DoesNotContain("ZGV2", issue.ToString());
        Assert.Equal("configured", Outcome(With((AccountKey, "rosemarydev"), (DevVaultKey, DevString)), config =>
        {
            Assert.DoesNotContain("ZGV2", Newtonsoft.Json.JsonConvert.SerializeObject(config));
            Assert.DoesNotContain("ZGV2", config.ConnectionString.ToString());
        }));
    }

    [Fact]
    public void Driver_loads_from_the_fabricated_emulator_string()
    {
        var driver = StorageAccountConfiguration.Load<StorageTablesConfiguration, AzureTableDriverDynamic>(
            With((AccountKey, "devstoreaccount1"), (PortKey, "11000")).Read,
            config => config.LoadDriver(),
            (issue, _) => throw new Exception(issue.ToString()),
            issue => throw new Exception(issue.ToString()),
            issue => throw new Exception(issue.ToString()));
        Assert.Equal(11002, driver.TableClient.BaseUri.Port);
        Assert.Equal(11000, driver.BlobClient.Uri.Port);
    }

    [Fact]
    public void FromSettings_default_key_routes_through_the_typed_configuration()
    {
        TestConfiguration.Ensure();
        var driver = AzureTableDriverDynamic.FromSettings();
        Assert.Equal(10002, driver.TableClient.BaseUri.Port);
        Assert.Equal(10002, AzureTableDriverDynamic.FromSettings(EastFive.Azure.AppSettings.Persistence.StorageTables.ConnectionString).TableClient.BaseUri.Port);
    }
}
