# EastFive

Functional-programming libraries for .NET: a composable core (TResult continuations,
`IEnumerableAsync`, attribute-interface IoC), an attribute-routed REST API framework,
Azure persistence/auth/integration bindings, spreadsheet tooling, and Roslyn source
generators.

| Package | Description |
|---|---|
| `EastFive.Core` | Functional primitives, LINQ extensions, async collections, configuration, reflection |
| `EastFive.Api` | Attribute-routed HTTP API framework for ASP.NET Core |
| `EastFive.Azure` | Azure Storage/Table persistence, auth (OAuth/SAML/Ping), integrations |
| `EastFive.Sheets` | XLSX/CSV reading and writing |
| `EastFive.Generators` | Roslyn source generators (flow extensions, storage query helpers, test harness) |

All packages ship one shared version, MIT-licensed, with embedded PDBs and embedded
source (step-into debugging works without symbol servers).

This mono-repo supersedes the former EastFive.Core, EastFive.Api, EastFive.Azure,
EastFive.Sheets, and EastFive.Generators repositories; their histories are preserved
under `src/`.

## Build

```sh
dotnet build EastFive.sln
dotnet test EastFive.sln   # Azure tests require a running Azurite
```

## Configuration and secrets

EastFive reads configuration from **two stores** and never mixes them.

| Store | What | Where |
|---|---|---|
| **Values** | Non-secret settings: endpoints, ids, account names, flags | `IConfiguration` (appsettings files, environment), initialized with `ConfigurationExtensions.Initialize` |
| **Secrets** | Keys, client secrets, connection strings | `ISecretStore`, queried by name — never an `IConfiguration` provider |

### Typed configuration is the only way to read a secret

Every setting a feature needs is a property of an `IProvideConfiguration` type. Values are
declared with their key; secrets are declared **without** one:

```csharp
[ConfigurationType("Telnyx")]
public sealed class TelnyxConfiguration : IProvideConfiguration
{
    [ConfigurationMember("Telnyx.Account", Account = true, Label = "Account")]
    public string Account { get; set; }

    [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "API key")]
    public Secret ApiKey { get; set; }
}

var outcome = await ConfigurationLoader.LoadAsync<TelnyxConfiguration, TResult>(
    onConfigured, onMissing, onInvalid, onSourceFailure);
```

`ConfigurationString` and friends are for values only; a guard test in each framework test
project fails if one reads a secret. `Secret` has no public value: `Reveal()` it at the point of
use, and never into a log, an exception or a response.

### Generated secret names

Secret names are never hand-written. `{Type}-{Account}-{Property}` for a type with an account
member, `{Type}-{Property}` otherwise: `Type` is the `[ConfigurationType]` name, `Account` the
account member's value, `Property` the secret member's property name. Names are valid Azure Key
Vault secret names (`[A-Za-z0-9-]`); an account id that would not be is refused, never mapped.
Types that share a `[ConfigurationType]` and an account share one secret — every
`StorageAccountConfiguration` on account `prod` reads `AzureStorage-prod-ConnectionString`.

`ConfigurationCatalog.For(assemblies)` lists every configuration type with its members,
metadata (label, description, location, scope, field kind) and generated names; editors and
doctors render from it. `IProvideConfigurationLookup` and `ITestConfiguration` are the
attribute-interface hooks for choices and live checks of a `ConfigurationDraft`.

### Where secrets come from

A host initializes `SecretStore.Current` next to `ConfigurationExtensions.Initialize`, typically:

```csharp
SecretStore.Initialize(new CompositeSecretStore(
    new ConfigurationSecretOverrides(ConfigurationLoader.ReadAmbient),   // Secrets:{name}
    KeyVaultSecretStore.From(vaults)));                                 // the environment's vaults, in order
```

1. **Local override**: `Secrets:{generated-name}` in the values store — written only in
   `appsettings.local*.json` or the environment (`Secrets__AzureStorage-dev-ConnectionString`).
2. **The environment's vaults**, in order: one `SecretClient` per vault, managed identity when
   deployed, a service principal (`KeyVaultAccess`) locally. 404 falls through to the next vault;
   any other error is a failure, so an outage is never read as "unset". Found and missing answers
   are cached per name for 30 minutes.

Until a host initializes it, `SecretStore.Current` is the local overrides alone.

### Synchronous readers: load at startup

Loading a type with secret members is asynchronous. Code that cannot await — attributes, the
storage driver factory, token signing — reads a configuration the host loaded once at startup:

```csharp
await StartupConfiguration.LoadAsync(new[] { typeof(EastFive.Azure.AppSettings).Assembly, /* … */ });
StartupConfiguration.Get<TokenSigningConfiguration, TResult>(onLoaded, onUnavailable);   // never loads, never blocks
```

A type the host did not load answers `ConfigIssueCategory.NotLoaded`. `RefreshEveryAsync` re-reads
the loaded types on an interval to pick up rotated secrets.

### Storage

A store is named by its `StorageAccountConfiguration` type (`StorageAccount.Tables`,
`StorageAccount.Of<T>()`); its account comes from `<Store>.Account` and its credential from
`AzureStorage-{account}-ConnectionString`. The emulator account `devstoreaccount1` is fabricated
from `Azure.Storage.Emulator.Port` and never read from the secret store.
