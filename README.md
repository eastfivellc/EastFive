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

`Secret` has no public value: `Reveal()` it at the point of use, and never into a log, an
exception or a response.

### All configuration is typed

Values are typed too. Every setting EastFive itself reads — secret or not — is a member of an
`IProvideConfiguration` type, and its key is a `{Member}Key` constant on that type
(`SessionServerConfiguration.LandingPageKey`). **There is no raw read API**: the
`ConfigurationString` family, `Settings.Get*`, `IProvideConfigurationKey` and the old `AppSettings`
key-constant classes are deleted, not deprecated. `ConfigurationExtensions.Initialize` installs the
ambient configuration a host built, and only the loader (`src/EastFive.Core/Configuration/`) reads
a key from it — `ConfigurationLoader.ReadAmbient` is internal. `Configured.ReadDeclaredValue` serves
the few APIs that take a key name, and only for a key some typed member declares. A guard test in
each framework test project asserts no assembly exposes a public raw read and no framework source
reads `IConfiguration`, `ConfigurationManager` or the environment directly (the one allowlisted
read is the vault credential choice in `KeyVaultSecretStore`, which runs before configuration
exists).

At the point of use, framework code reads through `Configured`:

```csharp
Configured.Get<CorsConfiguration, TResult>(cors => …, issue => …);
SessionServerConfiguration.WithLandingPage(uri => …, why => …);   // Configured.Member, callback-shaped
```

A **value-only** type is loaded synchronously from the ambient values on every read, so it is
always current; a type with **secret** members is served from the host's startup load and never
loaded there. Put a value on a secret-bearing type only when every reader of the value also needs
that secret — otherwise a missing secret would take the value down with it — and give it a
value-only type instead (`ApplicationInsightsTelemetryConfiguration` beside
`ApplicationInsightsQueryConfiguration`).

**Keyed families** — one instance per name, such as a Ping redirect or a SAML provider per tag —
implement `IProvideConfigurationInstanceKey`. Member keys carry a `{key}` placeholder, a
`FallbackKey` may name a former spelling, and `ConfigurationCatalog.InstanceKeys(type)` lists
the instances the configuration names.

A member that holds JSON accepts both a JSON string and a structured `appsettings.json` section
at its key.

**Every member is described.** A catalog-completeness test requires a label and description on
every type and member and a declared scope (on the member, or as the type's default via
`[ConfigurationType(…, Scope = …)]`); the SuperAdmin `Configuration` endpoint renders the catalog,
keyed instances included, and never a secret's value.

### Generated secret names

Secret names are never hand-written. `{Type}-{Account}-{Property}` for a type with an account
member, `{Type}-{Property}` otherwise: `Type` is the `[ConfigurationType]` name, `Account` the
account member's value, `Property` the secret member's property name. Names are valid Azure Key
Vault secret names (`[A-Za-z0-9-]`); an account id that would not be is refused, never mapped —
unless the account member declares an exact, type-specific translation with an attribute
implementing `IProvideSecretAccountSegment` (Google client ids drop their constant
`.apps.googleusercontent.com` domain). The translated segment is still validated.

**The account rule:** an account member used for secret naming must be vault-safe by FORMAT or
declare its translation. The catalog checks a draft value (`ConfigurationTypeDescription.
AccountProblem`) before it is written, and an unsafe declared default is a declaration error. A
vendor id that could ever be unsafe is never given a hand-picked alias member; it gets a
translation, so the vendor id stays the one value an operator enters.
Types that share a `[ConfigurationType]` and an account share one secret — every
`StorageAccountConfiguration` on account `prod` reads `AzureStorage-prod-ConnectionString`.

`ConfigurationCatalog.For(assemblies)` lists every configuration type with its members,
metadata (label, description, location, scope, field kind) and generated names; editors and
doctors render from it. `IProvideConfigurationLookup` and `ITestConfiguration` are the
attribute-interface hooks for choices and live checks of a `ConfigurationDraft`.
`ConfigurationLoader.InspectAsync(construct)` reports how each member of one type stands against
the ambient values and `SecretStore.Current` (names and sources, never values); the overloads
taking a `readValue` function inspect explicit sources instead.

### Generated values

A member whose value can be generated says so with an attribute implementing
`IGenerateConfigurationValue` (at most one per member; a second is a declaration error). The
configuration type decides which members have one; there is no framework allowlist.

```csharp
[ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "Engine secret")]
[GenerateRandomBytes(32)]                       // 43 characters, base64url
public Secret EngineSecret { get; init; } = default!;

[ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "Signing key")]
[GenerateRsaKey(2048, Format = ConfigurationRsaKeyFormat.RsaXmlBase64, KeyIdMember = nameof(KeyId),
    Instruction = "Upload the JWKS at the vendor's portal, then save.", InstructionLink = "https://…")]
public Secret SigningKey { get; init; } = default!;
```

Generation is READ-ONLY. `ConfigurationGeneration.GenerateAsync(context, onGenerated, onNoGenerator,
onRefused)` runs the member's generator over a `ConfigurationGenerationContext` (the type, the
member, the type's current draft by property name, the environment, the account or instance, and
injectable `Random` / `Clock` sources) and returns a `ConfigurationGenerationResult`:

- `Values` — member → value, for members of the SAME type only. A result naming any other member,
  or a value its member cannot hold, is refused (the reason names members, never values). A type's
  own generator may fill several members, e.g. the empty slot of a two-key rotation.
- `Outputs` — `{Label, Value, Kind: Text|Pem|Json}`, display-only and never stored: a public key, a
  JWKS entry, a key id to paste elsewhere.
- `Instructions` — `{Text, Link?}`, the steps to take outside configuration.

An editor puts the values in its drafts; the user saves them through the ordinary write paths.
The context carries draft secrets: never log it (its `ToString` names only the type and member).

| Built-in | Member value | Outputs |
| --- | --- | --- |
| `GenerateRandomBytes(length = 32)` | random bytes; `Encoding` `Base64Url` (default), `Base64`, `Hex` | — |
| `GenerateGuid` | a version 4 GUID; `Format` `D` (default), `N`, `B`, `P` | — |
| `GenerateAesKey(size = 256)` | 128/192/256-bit key; `Encoding` `Base64` (default) | — |
| `GenerateRsaKey(size = 2048)` | private key; `Format` `Pem` (PKCS#8, default), `Pkcs8Base64`, `RsaXmlBase64` (what `RSAFromSecret` reads) | public key PEM, JWKS (`Algorithm`, default RS256), key id |
| `GenerateEcKey(curve = P256)` | private key; `Format` `Pem` (default), `Pkcs8Base64`, `EcPrivateKeyBase64` (what `ECDSA.FromPrivateKey` reads) | public key PEM, JWKS (ES256/384/512 by curve), key id |

Every built-in takes `KeyIdMember` (a member of the same type that receives a dated id,
`yyyy-MM-dd-xxxxxxxx`, which is also the JWK `kid`; without it the key id is an output) and
`Instruction` / `InstructionLink`. `TokenSigningConfiguration.SigningKey` and
`VoucherConfiguration.SigningKey` declare `GenerateRsaKey(Format = RsaXmlBase64)`. Pick the format
the consuming code parses, and test the round trip. Rotation overlap (two active keys) belongs to
the consuming code, expressed as members of its own type.

### Adopting library configuration

An assembly that USES a configuration type declared in a library it references adopts it, so an
editor groups the type under the code that uses it instead of the library that declares it:

```csharp
[assembly: AdoptsConfiguration(typeof(StorageTablesConfiguration),
    Description = "Workflow runs, their state and the dispatch queues.", Required = true)]
```

Adoption is presentation only. The adopter may set `Label`, `Description` (its usage text),
`Location`, `MoreInfo` and `Required` (missing values make the type incomplete for that app, not
merely unconfigured). Keys, secret names, defaults and validation stay the library's, because
the library still reads its own keys; there is deliberately no way to override a default.
`ConfigurationAdoptions.Of(assembly)` lists an assembly's adoptions and refuses a malformed one
(not a concrete configuration type, declared by the adopter itself, from an assembly it does not
reference, or adopted twice). `ConfigurationGraph.Of(roots)` walks a host's reference graph —
roots first, then by distance, ties by root order — and `Place(type)` puts each type under the
adopter closest to the roots, listing the other adopters as also using it; a type nobody in the
graph adopts stays under its declaring assembly.

### Where secrets come from

A host initializes `SecretStore.Current` next to `ConfigurationExtensions.Initialize`, typically:

```csharp
SecretStore.Initialize(new CompositeSecretStore(
    SecretStore.AmbientOverrides,                                       // Secrets:{name}
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
await StartupConfiguration.LoadAsync(new[] { typeof(EastFive.Azure.Persistence.StorageAccount).Assembly, /* … */ });
StartupConfiguration.Get<TokenSigningConfiguration, TResult>(onLoaded, onUnavailable);   // never loads, never blocks
```

A type the host did not load answers `ConfigIssueCategory.NotLoaded`. `RefreshEveryAsync` re-reads
the loaded types on an interval to pick up rotated secrets.

### Storage

A store is named by its `StorageAccountConfiguration` type (`StorageAccount.Tables`,
`StorageAccount.Of<T>()`); its account comes from `<Store>.Account` and its credential from
`AzureStorage-{account}-ConnectionString`. The emulator account `devstoreaccount1` is fabricated
from `Azure.Storage.Emulator.Port` and never read from the secret store.
