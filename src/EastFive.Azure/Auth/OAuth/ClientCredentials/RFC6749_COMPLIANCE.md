# RFC 6749 Section 2 Client Registration Compliance

## Overview

The `ClientCredential` resource has been updated to be fully compliant with OAuth 2.0 RFC 6749 Section 2: Client Registration.

## RFC 6749 Section 2 Requirements

### 2.1 Client Types

**RFC Requirement:** OAuth defines two client types based on their ability to authenticate securely:
- **Confidential**: Capable of maintaining credential confidentiality
- **Public**: Incapable of maintaining credential confidentiality

**Implementation:**
- Added `clientType` property (REQUIRED)
- Values: `"confidential"` or `"public"`
- Validation enforced in `ValidateRegistration()` method

### 2.2 Client Identifier

**RFC Requirement:** Authorization server issues a unique client identifier that:
- Is NOT a secret
- Is exposed to the resource owner
- MUST NOT be used alone for client authentication

**Implementation:**
- `clientId` property with `[StorageConstraintUnique]` attribute
- Unique string representing registration information
- Exposed in API responses

### 2.3 Client Authentication

**RFC Requirements:**
- Confidential clients establish client authentication method
- Public clients MUST NOT use client authentication for identification
- Server MAY accept any form of authentication meeting security requirements

**Implementation:**
- `clientSecret` property (REQUIRED for confidential, NULL for public)
- `tokenEndpointAuthMethod` property with supported values:
  - `"client_secret_basic"` - HTTP Basic authentication (RFC 2617)
  - `"client_secret_post"` - Form-encoded body parameters
  - `"none"` - No authentication (public clients only)

### 2.3.1 Client Password

**RFC Requirements:**
- Clients MAY use HTTP Basic authentication
- Authorization server MUST support HTTP Basic auth for clients issued a password
- Parameters: `client_id` and `client_secret`

**Implementation:**
- `clientSecret` is stored as a hash (`OAuthServer.ComputeSecretHash`, SHA-256 base64url);
  the plaintext is returned EXACTLY once, from create / `rotate-secret`, and is never
  serialized afterwards (`[JsonIgnore]`).
- Verification is constant-time (`OAuthServer.SecretMatchesHash` →
  `CryptographicOperations.FixedTimeEquals`) through one matcher,
  `ClientCredential.MatchesSecret`, used by both `/oauth/token` and the admin
  `/authenticate` action.
- Both `client_secret_basic` (RFC 6749 §2.3.1 — id and secret form-urlencoded, then
  HTTP Basic) and `client_secret_post` are accepted at the token endpoint
  (`OAuthToken.ReadClientAuthentication`).
- Rows written before hashing (plaintext `clientSecret`) still authenticate against the
  primary slot; they are upgraded to a hash on their next `rotate-secret`.

### Secret rotation with a grace period (dual secrets)

A confidential client has two hashed slots:

| Slot | Field | Accepted at `/oauth/token` | Serialized |
|------|-------|----------------------------|------------|
| current | `clientSecret` | yes | never |
| previous | `clientSecretSecondary` | yes, until retired or displaced | never |

- `POST /OAuth/ClientCredential/{id}/rotate-secret` (`[SuperAdminClaim]`) generates a new
  current secret (returned in plaintext once, with `previous_secret_retained: true`) and
  moves the old current hash into `clientSecretSecondary`. A fleet can roll to the new
  secret without a window where token requests fail.
- `POST /OAuth/ClientCredential/{id}/retire-secondary` (`[SuperAdminClaim]`) clears the
  previous slot, ending the grace period. Idempotent.
- A second `rotate-secret` displaces the previous slot, so the oldest secret is dropped —
  at most two secrets are ever live.
- The previous slot is hash-only (no legacy plaintext fallback).

### 2.4 Registration Requirements

**RFC Requirements:** When registering, the client developer SHALL:
1. Specify the client type (Section 2.1)
2. Provide client redirection URIs (Section 3.1.2)
3. Include other required information (name, description, etc.)

**Implementation:**
- `ValidateRegistration()` method enforces all RFC requirements
- Validates client type
- Validates redirect_uris for authorization/implicit flows
- Validates grant types
- Ensures confidential/public client rules are followed

## New Properties Added

### Required Properties

| Property | Type | Description | RFC Reference |
|----------|------|-------------|---------------|
| `clientType` | string | "confidential" or "public" | RFC 6749 §2.1 |
| `tokenEndpointAuthMethod` | string | Authentication method for token endpoint | RFC 6749 §2.3 |
| `grantTypes` | string | Comma-separated list of authorized grant types | RFC 6749 §4 |

### Updated Properties

| Property | Previous | Updated | RFC Reference |
|----------|----------|---------|---------------|
| `clientSecret` | Required | Optional (NULL for public clients) | RFC 6749 §2.3 |
| `redirectUris` | Optional | Required for public clients and auth code/implicit flows | RFC 6749 §3.1.2 |

## Validation Rules

### Client Type Validation

```csharp
// Confidential clients
- MUST have clientSecret
- MUST have tokenEndpointAuthMethod (not 'none')
- May use any authentication method

// Public clients
- MUST NOT have clientSecret
- tokenEndpointAuthMethod must be 'none' or empty
- MUST have redirectUris if using authorization/implicit flows
```

### Redirect URI Validation

Per RFC 6749 Section 3.1.2:
- Must be absolute URIs
- MUST NOT include fragment components
- Required for:
  - All public clients
  - Confidential clients using authorization_code grant
  - Confidential clients using implicit grant

### Grant Type Validation

Supported grant types per RFC 6749:
- `authorization_code` - Authorization Code Grant (§4.1)
- `implicit` - Implicit Grant (§4.2)
- `password` - Resource Owner Password Credentials Grant (§4.3)
- `client_credentials` - Client Credentials Grant (§4.4)
- `refresh_token` - Refresh Token (§6)

## API Changes

### POST /OAuth/ClientCredential (Create)

**New Required Parameters:**
- `client_type` - "confidential" or "public"

**New Optional Parameters:**
- `grant_types` - Comma-separated list
- `token_endpoint_auth_method` - Authentication method

**Updated Parameters:**
- `client_secret` - Now optional (required only for confidential clients)

**Example: Confidential Client Registration**

```json
{
  "client_id": "my-server-app",
  "client_type": "confidential",
  "client_secret": "secretvalue",
  "token_endpoint_auth_method": "client_secret_basic",
  "name": "My Server Application",
  "redirect_uris": "https://myapp.com/callback",
  "grant_types": "authorization_code,refresh_token",
  "scope": "read write"
}
```

**Example: Public Client Registration**

```json
{
  "client_id": "my-spa-app",
  "client_type": "public",
  "token_endpoint_auth_method": "none",
  "name": "My Single Page Application",
  "redirect_uris": "https://myapp.com/callback",
  "grant_types": "authorization_code",
  "scope": "read"
}
```

## Client Credentials Grant (RFC 6749 §4.4) at `POST /oauth/token`

For admin-provisioned machine clients (e.g. a CLI calling a deployed service). No user
account is involved.

**Request** — `application/x-www-form-urlencoded`, client authenticated by HTTP Basic
(preferred) or `client_id`/`client_secret` in the body:

```
grant_type=client_credentials&scope=rosemary.engine
```

**Checks, in order, and the RFC 6749 §5.2 error each produces:**

| Condition | `error` |
|-----------|---------|
| `grant_type` missing | `invalid_request` |
| unknown `client_id`, wrong secret, or inactive client | `invalid_client` |
| `grant_type` present but not supported | `unsupported_grant_type` |
| client is not `confidential`, or no secret presented | `unauthorized_client` |
| `grant_types` registration lacks `client_credentials` | `unauthorized_client` |
| requested `scope` not a subset of the registered `scope` | `invalid_scope` |

**Response** — `access_token` (RSA JWT signed like session tokens), `token_type: Bearer`,
`expires_in` (seconds; `EastFive.Azure.OAuth.AccessTokenExpirationInMinutes`, default 60),
`scope` (the requested scope, or the registered scope when none was requested). No refresh
token (OAuth 2.1: the client can simply re-authenticate). `Cache-Control: no-store`.

**What the token can reach.** The JWT carries only `client_id` and `scp` claims. It passes
`[RequiredScope("x")]` gates whose scope is in `scp` (403 `insufficient_scope` otherwise)
and does NOT satisfy account/role gates such as `[SuperAdminClaim]` — a machine identity is
not a user. Session-token-only endpoints stay closed to it by construction.

### Client-side helper: `EastFive.Api.Auth.ClientCredentialsHandler`

A `DelegatingHandler` (in `EastFive.Api`, no Azure dependency) that gives an `HttpClient`
a client-credentials identity:

```csharp
var handler = ClientCredentialsHandler.FromConfiguration(
    "Rosemary.TokenEndpoint", "Rosemary.ClientId", "Rosemary.ClientSecret", "Rosemary.Scope");
var http = new HttpClient(handler) { BaseAddress = rosemaryBaseUri };
```

- Lazily POSTs the grant with HTTP Basic auth; caches the token until `exp − 60s`
  (`RefreshGuardBand`); coalesces concurrent first fetches.
- On a downstream 401: refreshes once, retries once (body buffered); a second 401 is returned.
- Token-endpoint failures throw `ClientCredentialsTokenException` with `Error` /
  `ErrorDescription` (§5.2), the HTTP status, and the raw body.
- `FromConfiguration` resolves through `ConfigurationString`, so `ClientId` can live in
  appsettings and `ClientSecret` in Key Vault; the scope key is optional.

## Helper Methods

### ClientCredential.ValidateRegistration()

Validates all RFC 6749 Section 2 requirements:
- Client type validity
- Client secret requirements based on type
- Redirect URI requirements
- Grant type validity
- Token endpoint authentication method

### ClientCredential.IsAuthorizedForGrantType()

Checks if a client is authorized to use a specific grant type.

### Static Classes

#### ClientTypes
- `Confidential` - Constant for "confidential"
- `Public` - Constant for "public"
- `IsValid()` - Validates client type value

#### GrantTypeValues
- Constants for all valid grant types
- `IsValid()` - Validates grant type
- `All` - Array of all valid grant types

#### TokenEndpointAuthMethods
- Constants for valid authentication methods
- `IsValid()` - Validates auth method
- `All` - Array of all valid methods

## Security Considerations

### Client Secret Storage

Per RFC 6749 Section 10.1:
- Client secrets are hashed before storage (`OAuthServer.ComputeSecretHash`) and
  compared in constant time (`ClientCredential.MatchesSecret`)
- The plaintext is returned exactly once (create / `rotate-secret`) and never serialized
- Rotation retains the previous hash for a grace period; `retire-secondary` ends it
  (see "Secret rotation with a grace period" above)

### Public Client Security

Per RFC 6749 Section 2.1:
- Public clients (SPAs, native apps) cannot maintain secret confidentiality
- Do NOT issue client secrets to public clients
- Use PKCE (RFC 7636) for additional security (future enhancement)

### Credential Guessing Attacks

Per RFC 6749 Section 10.10:
- Client IDs and secrets use cryptographically secure random generation
- 128-bit (16 byte) client IDs
- 256-bit (32 byte) client secrets

## Migration Guide

### Existing Clients

Existing `ClientCredential` records without the new fields will need migration:

1. **Set clientType:** Determine if each client is confidential or public
2. **Set tokenEndpointAuthMethod:** Based on client type
3. **Set grantTypes:** Based on intended use

**Example Migration Script:**

```csharp
// Mark existing clients as confidential (they all have secrets)
foreach (var client in existingClients)
{
    client.clientType = "confidential";
    client.tokenEndpointAuthMethod = "client_secret_post";
    client.grantTypes = "client_credentials,refresh_token";
    await client.UpdateAsync();
}
```

### API Consumers

API consumers creating new clients MUST now provide:
- `client_type` parameter
- Follow validation rules for confidential vs public clients
- Provide `token_endpoint_auth_method` for confidential clients

## References

- [RFC 6749 - OAuth 2.0 Authorization Framework](https://datatracker.ietf.org/doc/html/rfc6749)
- [RFC 6749 Section 2 - Client Registration](https://datatracker.ietf.org/doc/html/rfc6749#section-2)
- [RFC 6749 Section 2.1 - Client Types](https://datatracker.ietf.org/doc/html/rfc6749#section-2.1)
- [RFC 6749 Section 2.2 - Client Identifier](https://datatracker.ietf.org/doc/html/rfc6749#section-2.2)
- [RFC 6749 Section 2.3 - Client Authentication](https://datatracker.ietf.org/doc/html/rfc6749#section-2.3)
- [RFC 6749 Section 3.1.2 - Redirection Endpoint](https://datatracker.ietf.org/doc/html/rfc6749#section-3.1.2)

## Future Enhancements

1. **PKCE Support** (RFC 7636) - Proof Key for Code Exchange for public clients
2. **Dynamic Client Registration** (RFC 7591) - Automated client registration endpoint
3. **Client Metadata** (RFC 7591) - Additional standardized metadata fields
4. **JWT Client Authentication** (RFC 7523) - JWT-based client authentication
