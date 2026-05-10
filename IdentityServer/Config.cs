using Duende.IdentityServer;
using Duende.IdentityServer.Models;

namespace IdentityServer;

public static class Config
{
    public static IEnumerable<IdentityResource> IdentityResources =>
        new IdentityResource[]
        {
            new IdentityResources.OpenId(),
            new IdentityResources.Profile(),
            new IdentityResources.Email(),
            // Custom identity resource for account context
            new IdentityResource(
                name: "account",
                displayName: "Account Information",
                userClaims: new[] 
                { 
                    "account_id",
                    "account_version",
                    "available_accounts"
                })
        };

    // API Resources define the APIs in your system and their audiences
    public static IEnumerable<ApiResource> ApiResources =>
        new ApiResource[]
        {
            new ApiResource("talabat.api", "Talabat API")
            {
                Scopes = { "talabat.api" },
                UserClaims = { "account_id", "account_version" }
            },
            new ApiResource("identity.api", "Identity Server API")
            {
                Scopes = { "users.read" }
            }
        };

    public static IEnumerable<ApiScope> ApiScopes =>
        new ApiScope[]
        {
            // Unified Talabat API scope (serves shop, customer, and admin operations)
            new ApiScope("talabat.api", "Talabat API")
            {
                UserClaims = { "account_id", "account_version" }
            },
            // Scope for reading users list from Identity Server
            new ApiScope("users.read", "Read Users")
        };

    // Custom grant type for token exchange (RFC 8693)
    private const string TokenExchangeGrantType = "urn:ietf:params:oauth:grant-type:token-exchange";

    public static IEnumerable<Client> Clients =>
        new Client[]
        {
            // Talabat Web Application (unified client for customer, shop, admin)
            new Client
            {
                ClientId = "talabat.web",
                ClientName = "Talabat Web Application",
                ClientSecrets = { new Secret("49C1A7E1-0C79-4A89-A3D6-A37998FB86B0".Sha256()) },

                AllowedGrantTypes = GrantTypes.CodeAndClientCredentials
                    .Append(TokenExchangeGrantType).ToList(),
                RequirePkce = true,

                RedirectUris = { "https://localhost:5002/signin-oidc" },
                PostLogoutRedirectUris = { "https://localhost:5002/signout-callback-oidc" },

                // Back-channel logout support
                BackChannelLogoutUri = "https://localhost:5002/api/backchannel-logout",
                BackChannelLogoutSessionRequired = true,

                AllowOfflineAccess = true,
                AllowedScopes = 
                { 
                    IdentityServerConstants.StandardScopes.OpenId,
                    IdentityServerConstants.StandardScopes.Profile,
                    IdentityServerConstants.StandardScopes.Email,
                    IdentityServerConstants.StandardScopes.OfflineAccess,
                    "account",
                    "talabat.api"
                },

                // Token lifetime - shorter access tokens, 24h refresh with rotation
                AccessTokenLifetime = 900, // 15 minutes
                RefreshTokenUsage = TokenUsage.OneTimeOnly, // Rotation on each refresh
                RefreshTokenExpiration = TokenExpiration.Sliding,
                SlidingRefreshTokenLifetime = 86400, // 24 hours
                AbsoluteRefreshTokenLifetime = 86400 * 7 // Max 7 days
            },
            new Client
            {
                ClientId = "talabat.admin",
                ClientName = "Talabat Admin Application",
                ClientSecrets = { new Secret("49C1A7E1-0C79-4A89-A3D6-A37998FB86B0".Sha256()) },

                AllowedGrantTypes = GrantTypes.CodeAndClientCredentials
                    .Append(TokenExchangeGrantType).ToList(),
                RequirePkce = true,

                // Include both URIs - port 4200 (Angular dev) and port 7082 (BFF)
                RedirectUris =
                {
                    "https://localhost:7082/signin-oidc",
                    "https://localhost:4200/signin-oidc"
                },
                PostLogoutRedirectUris =
                {
                    "https://localhost:7082/signout-callback-oidc",
                    "https://localhost:4200/signout-callback-oidc"
                },

                BackChannelLogoutUri = "https://localhost:7082/bff/backchannel",
                BackChannelLogoutSessionRequired = true,

                AllowOfflineAccess = true,
                AllowedScopes =
                {
                    IdentityServerConstants.StandardScopes.OpenId,
                    IdentityServerConstants.StandardScopes.Profile,
                    IdentityServerConstants.StandardScopes.Email,
                    IdentityServerConstants.StandardScopes.OfflineAccess,
                    "account",
                    "talabat.api"
                },

                // Token lifetime - shorter access tokens, 24h refresh with rotation
                AccessTokenLifetime = 900, // 15 minutes
                RefreshTokenUsage = TokenUsage.OneTimeOnly, // Rotation on each refresh
                RefreshTokenExpiration = TokenExpiration.Sliding,
                SlidingRefreshTokenLifetime = 86400, // 24 hours
                AbsoluteRefreshTokenLifetime = 86400 * 7 // Max 7 days
            },

            // API Server - machine-to-machine client for user validation
            new Client
            {
                ClientId = "talabat.api.server",
                ClientName = "Talabat API Server",
                ClientSecrets = { new Secret("B5A3C8E2-1F47-4D6B-9E8A-7C2D5F1A3B09".Sha256()) },

                AllowedGrantTypes = GrantTypes.ClientCredentials,

                AllowedScopes =
                {
                    "users.read"
                }
            },

            // Swagger UI client (for API documentation/testing)
            new Client
            {
                ClientId = "swagger.ui",
                ClientName = "Swagger UI",
                RequireClientSecret = false, // Public client (PKCE)

                AllowedGrantTypes = GrantTypes.Code
                    .Append(TokenExchangeGrantType).ToList(),
                RequirePkce = true,

                // Allow any redirect for localhost development
                RedirectUris =
                { 
                    "https://localhost:5001/swagger/oauth2-redirect.html",
                    "https://localhost:5001/swagger/o2c.html"
                },
                PostLogoutRedirectUris = { "https://localhost:5001/swagger" },

                AllowedCorsOrigins = { "https://localhost:5001" },

                AllowOfflineAccess = true, // Allow refresh tokens
                AllowedScopes = 
                { 
                    IdentityServerConstants.StandardScopes.OpenId,
                    IdentityServerConstants.StandardScopes.Profile,
                    IdentityServerConstants.StandardScopes.Email,
                    IdentityServerConstants.StandardScopes.OfflineAccess,
                    "account",
                    "talabat.api"
                },

                // Token lifetimes
                AccessTokenLifetime = 300, // 5 minutes for testing
                RefreshTokenUsage = TokenUsage.OneTimeOnly,
                RefreshTokenExpiration = TokenExpiration.Sliding,
                SlidingRefreshTokenLifetime = 3600, // 1 hour
                AbsoluteRefreshTokenLifetime = 86400 // 24 hours max
            }
        };
}
