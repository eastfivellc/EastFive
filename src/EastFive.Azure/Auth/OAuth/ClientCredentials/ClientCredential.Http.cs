using System;
using System.Linq;
using System.Threading.Tasks;
using EastFive.Api;
using MutateEntityAttribute = EastFive.Api.Binding.MutateEntityAttribute;
using EastFive.Api.Serialization.Json;
using EastFive.Azure.Auth;
using EastFive.Azure.Persistence;
using EastFive.Azure.Persistence.AzureStorageTables;
using EastFive.Extensions;
using EastFive.Linq.Async;
using Newtonsoft.Json;

namespace EastFive.Azure.OAuth
{
    /// <summary>
    /// HTTP API for managing OAuth 2.0 Client Credentials (RFC 6749 Section 4.4)
    /// Stores and allows HTTP access to a list of external clients which authenticate using client credential flows.
    /// </summary>
    [FunctionViewController(
        Route = "OAuth/ClientCredential",
        ContentType = "application/x-oauth-clientcredential+json")]
    public partial struct ClientCredential
    {
        /// <summary>
        /// GET /OAuth/ClientCredential - Get all client credentials
        /// </summary>
        [HttpGet]
        [SuperAdminClaim]
        public static IHttpResponse GetAllAsync(
                RequestMessage<ClientCredential> clientsQuery,
            MultipartAsyncResponse<ClientCredential> onResults)
        {
            return clientsQuery
                .StorageGet()
                .HttpResponse(onResults);
        }

        /// <summary>
        /// GET /OAuth/ClientCredential/{id} - Get specific client by ID
        /// </summary>
        [HttpGet]
        [SuperAdminClaim]
        public static async Task<IHttpResponse> GetByIdAsync(
                [UpdateId] IRef<ClientCredential> clientRef,
            ContentTypeResponse<ClientCredential> onFound,
            NotFoundResponse onNotFound)
        {
            return await clientRef.StorageGetAsync(
                client => onFound(client),
                () => onNotFound());
        }

        /// <summary>
        /// POST /OAuth/ClientCredential - Create new OAuth 2.0 client registration (RFC 6749 Section 2).
        /// client_id and (for confidential clients) client_secret are OPTIONAL: when omitted they are
        /// generated server-side (id = record GUID "N", the DCR convention; secret = cryptographically
        /// random, stored hashed). A generated secret is returned in the response's client_secret
        /// EXACTLY once — it can never be retrieved again.
        /// </summary>
        [HttpPost]
        [SuperAdminClaim]
        public static async Task<IHttpResponse> CreateAsync(
                [UpdateId] IRef<ClientCredential> clientRef,
                [PropertyOptional(Name = ClientIdPropertyName)] string clientId,
                [Property(Name = ClientTypePropertyName)] string clientType,
                [PropertyOptional(Name = ClientSecretPropertyName)] string clientSecret,
                [Property(Name = NamePropertyName)] string name,
                [PropertyOptional(Name = DescriptionPropertyName)] string description,
                [PropertyOptional(Name = ScopePropertyName)] string scope,
                [PropertyOptional(Name = RedirectUrisPropertyName)] string redirectUris,
                [PropertyOptional(Name = GrantTypesPropertyName)] string grantTypes,
                [PropertyOptional(Name = TokenEndpointAuthMethodPropertyName)] string tokenEndpointAuthMethod,
                [PropertyOptional(Name = IsActivePropertyName)] bool? isActive,
                [PropertyOptional(Name = ContactEmailPropertyName)] string contactEmail,
                [Resource] ClientCredential client,
            CreatedBodyResponse<ClientCredentialCreatedResponse> onCreated,
            AlreadyExistsResponse onAlreadyExists,
            BadRequestResponse onBadRequest)
        {
            // Validate required fields per RFC 6749 Section 2
            if (string.IsNullOrWhiteSpace(clientType))
                return onBadRequest().AddReason("client_type is required (must be 'confidential' or 'public')");

            if (string.IsNullOrWhiteSpace(name))
                return onBadRequest().AddReason("name is required");

            // Set defaults and audit timestamps
            client.isActive = isActive ?? true;
            client.createdAt = DateTime.UtcNow;
            client.updatedAt = DateTime.UtcNow;
            client.lastUsedAt = null;

            // Secrets are stored hashed; the caller already holds the plaintext it sent.
            if (!string.IsNullOrWhiteSpace(clientSecret))
                client.clientSecret = Server.OAuthServer.ComputeSecretHash(clientSecret);

            // Server-generated client_id / client_secret / auth-method defaults.
            client = ApplyProvisioningDefaults(client, out var generatedSecretMaybe);

            // Validate registration per RFC 6749 Section 2
            return await client.ValidateRegistration(
                () =>
                {
                    return client.StorageCreateAsync(
                        created => onCreated(
                            ClientCredentialCreatedResponse.FromClient(created.Entity, generatedSecretMaybe)),
                        onAlreadyExists: () => onAlreadyExists());
                },
                invalidReason => onBadRequest().AddReason(invalidReason).AsTask());
        }

        /// <summary>
        /// PATCH /OAuth/ClientCredential/{id} - Update existing client credential
        /// </summary>
        [HttpPatch]
        [SuperAdminClaim]
        public static async Task<IHttpResponse> UpdateAsync(
            [EastFive.Api.Binding.UpdateId] IRef<ClientCredential> clientRef,
            [MutateEntity] MutateResource<ClientCredential> mutateResource,
            ContentTypeResponse<ClientCredential> onUpdated,
            NotFoundResponse onNotFound)
        {
            return await clientRef.StorageUpdateAsync2(
                client =>
                {
                    client.updatedAt = DateTime.UtcNow;
                    var updates = mutateResource(client);
                    return updates;
                },
                (updatedClient) => onUpdated(updatedClient),
                () => onNotFound());
        }

        /// <summary>
        /// DELETE /OAuth/ClientCredential/{id} - Delete client credential
        /// </summary>
        [HttpDelete]
        [SuperAdminClaim]
        public static async Task<IHttpResponse> DeleteAsync(
                [UpdateId] IRef<ClientCredential> clientRef,
            NoContentResponse onDeleted,
            NotFoundResponse onNotFound)
        {
            return await clientRef.StorageDeleteAsync(
                deleted => onDeleted(),
                () => onNotFound());
        }

        /// <summary>
        /// POST /OAuth/ClientCredential/authenticate - Authenticate a client using client credentials
        /// Implements RFC 6749 Section 4.4: Client Credentials Grant
        /// This endpoint validates client_id and client_secret and returns success/failure
        /// </summary>
        [HttpAction("POST", "authenticate")]
        [SuperAdminClaim]
        public static async Task<IHttpResponse> AuthenticateAsync(
                [Property(Name = ClientIdPropertyName)] string clientId,
                [Property(Name = ClientSecretPropertyName)] string clientSecret,
            ContentTypeResponse<ClientAuthenticationResponse> onAuthenticated,
            UnauthorizedResponse onUnauthorized,
            BadRequestResponse onBadRequest)
        {
            if (string.IsNullOrWhiteSpace(clientId))
                return onBadRequest().AddReason("client_id is required");

            if (string.IsNullOrWhiteSpace(clientSecret))
                return onBadRequest().AddReason("client_secret is required");

            // Find client by clientId using StorageGetBy with unique constraint
            var matchingClients = await clientId.StorageGetBy(
                (ClientCredential client) => client.clientId)
                .ToArrayAsync();

            if (matchingClients.Length == 0)
                return onUnauthorized().AddReason("Invalid client_id");

            var client = matchingClients.First();

            // Validate client is active
            if (!client.isActive)
                return onUnauthorized().AddReason("Client is not active");

            // Validate client secret (current or grace-period secondary; legacy plaintext fallback)
            if (!client.MatchesSecret(clientSecret))
                return onUnauthorized().AddReason("Invalid client credentials");

            // Update last used timestamp (fire and forget for performance)
            _ = client.@ref.StorageUpdateAsync2(
                c =>
                {
                    c.lastUsedAt = DateTime.UtcNow;
                    c.updatedAt = DateTime.UtcNow;
                    return c;
                },
                _ => true,
                () => false);

            // Return authentication success
            var response = new ClientAuthenticationResponse
            {
                ClientId = client.clientId,
                Name = client.name,
                Scope = client.scope,
                IsActive = client.isActive
            };

            return onAuthenticated(response);
        }

        /// <summary>
        /// POST /OAuth/ClientCredential/{id}/rotate-secret - Rotate client secret.
        /// Generates a new primary secret (returned in plaintext EXACTLY once) and moves the
        /// previous primary into the secondary slot, where the token endpoint keeps accepting
        /// it until <c>retire-secondary</c> is called or the next rotation displaces it.
        /// A second rotation therefore drops the oldest secret.
        /// </summary>
        [HttpAction("POST", "rotate-secret")]
        [SuperAdminClaim]
        public static async Task<IHttpResponse> RotateSecretAsync(
                [UpdateId] IRef<ClientCredential> clientRef,
            ContentTypeResponse<ClientSecretResponse> onRotated,
            NotFoundResponse onNotFound)
        {
            // Cryptographically random; stored hashed, returned in plaintext exactly once.
            var newSecret = Server.OAuthServer.GenerateSecret();
            return await clientRef.StorageUpdateAsync2(
                client =>
                {
                    if (client.clientSecret.HasBlackSpace())
                        client.clientSecretSecondary = client.clientSecret;
                    client.clientSecret = Server.OAuthServer.ComputeSecretHash(newSecret);
                    client.updatedAt = DateTime.UtcNow;

                    return client;
                },
                updatedClient =>
                {
                    var response = new ClientSecretResponse
                    {
                        ClientId = updatedClient.clientId,
                        ClientSecret = newSecret,
                        UpdatedAt = updatedClient.updatedAt,
                        PreviousSecretRetained = updatedClient.clientSecretSecondary.HasBlackSpace(),
                    };
                    return onRotated(response);
                },
                () => onNotFound());
        }

        /// <summary>
        /// POST /OAuth/ClientCredential/{id}/retire-secondary - End the rotation grace period:
        /// clears the retained previous secret so only the current one authenticates.
        /// Idempotent.
        /// </summary>
        [HttpAction("POST", "retire-secondary")]
        [SuperAdminClaim]
        public static async Task<IHttpResponse> RetireSecondarySecretAsync(
                [UpdateId] IRef<ClientCredential> clientRef,
            ContentTypeResponse<ClientCredential> onRetired,
            NotFoundResponse onNotFound)
        {
            return await clientRef.StorageUpdateAsync2(
                client =>
                {
                    client.clientSecretSecondary = default;
                    client.updatedAt = DateTime.UtcNow;
                    return client;
                },
                updatedClient => onRetired(updatedClient),
                () => onNotFound());
        }

        /// <summary>
        /// POST /OAuth/ClientCredential/{id}/activate - Activate a client
        /// </summary>
        [HttpAction("POST", "activate")]
        [SuperAdminClaim]
        public static async Task<IHttpResponse> ActivateAsync(
                [UpdateId] IRef<ClientCredential> clientRef,
            ContentTypeResponse<ClientCredential> onActivated,
            NotFoundResponse onNotFound)
        {
            return await clientRef.StorageUpdateAsync2(
                client =>
                {
                    client.isActive = true;
                    client.updatedAt = DateTime.UtcNow;
                    return client;
                },
                (updatedClient) => onActivated(updatedClient),
                () => onNotFound());
        }

        /// <summary>
        /// POST /OAuth/ClientCredential/{id}/deactivate - Deactivate a client
        /// </summary>
        [HttpAction("POST", "deactivate")]
        [SuperAdminClaim]
        public static async Task<IHttpResponse> DeactivateAsync(
                [UpdateId] IRef<ClientCredential> clientRef,
            ContentTypeResponse<ClientCredential> onDeactivated,
            NotFoundResponse onNotFound)
        {
            return await clientRef.StorageUpdateAsync2(
                client =>
                {
                    client.isActive = false;
                    client.updatedAt = DateTime.UtcNow;
                    return client;
                },
                (updatedClient) => onDeactivated(updatedClient),
                () => onNotFound());
        }
    }

    /// <summary>
    /// Response for successful client authentication
    /// </summary>
    public class ClientAuthenticationResponse
    {
        [JsonProperty("client_id")]
        public string ClientId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("scope")]
        public string Scope { get; set; }

        [JsonProperty("is_active")]
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// Response for client secret rotation
    /// </summary>
    public class ClientSecretResponse
    {
        [JsonProperty("client_id")]
        public string ClientId { get; set; }

        [JsonProperty("client_secret")]
        public string ClientSecret { get; set; }

        [JsonProperty("updated_at")]
        public DateTime UpdatedAt { get; set; }

        /// <summary>True when the previous secret is still accepted (call retire-secondary to end the grace period).</summary>
        [JsonProperty("previous_secret_retained")]
        public bool PreviousSecretRetained { get; set; }
    }

    /// <summary>
    /// Response for client creation — the ClientCredential wire shape plus, when the
    /// server generated the secret, its plaintext (returned EXACTLY once; stored hashed).
    /// </summary>
    public class ClientCredentialCreatedResponse
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("client_id")]
        public string ClientId { get; set; }

        [JsonProperty("client_type")]
        public string ClientType { get; set; }

        [JsonProperty("client_secret")]
        public string ClientSecret { get; set; }

        [JsonProperty("redirect_uris")]
        public string RedirectUris { get; set; }

        [JsonProperty("grant_types")]
        public string GrantTypes { get; set; }

        [JsonProperty("token_endpoint_auth_method")]
        public string TokenEndpointAuthMethod { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("scope")]
        public string Scope { get; set; }

        [JsonProperty("is_active")]
        public bool IsActive { get; set; }

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [JsonProperty("last_used_at")]
        public DateTime? LastUsedAt { get; set; }

        [JsonProperty("contact_email")]
        public string ContactEmail { get; set; }

        public static ClientCredentialCreatedResponse FromClient(
            ClientCredential client, string generatedSecretMaybe) =>
            new ClientCredentialCreatedResponse
            {
                Id = client.id,
                ClientId = client.clientId,
                ClientType = client.clientType,
                ClientSecret = generatedSecretMaybe,
                RedirectUris = client.redirectUris,
                GrantTypes = client.grantTypes,
                TokenEndpointAuthMethod = client.tokenEndpointAuthMethod,
                Name = client.name,
                Description = client.description,
                Scope = client.scope,
                IsActive = client.isActive,
                CreatedAt = client.createdAt,
                UpdatedAt = client.updatedAt,
                LastUsedAt = client.lastUsedAt,
                ContactEmail = client.contactEmail,
            };
    }
}
