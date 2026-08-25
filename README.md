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
