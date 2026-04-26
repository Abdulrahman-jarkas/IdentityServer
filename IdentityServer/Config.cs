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
                    "tenant_id", 
                    "tenant_name", 
                    "tenant_slug",
                    "tenant_type",
                    "account_name",
                    "role",
                    "permission",
                    "available_accounts"
                })
        };

    public static IEnumerable<ApiScope> ApiScopes =>
        new ApiScope[]
        {
            // Unified Talabat API scope (serves shop, customer, and admin operations)
            new ApiScope("talabat.api", "Talabat API")
            {
                UserClaims = { "account_id", "tenant_id", "tenant_type", "role", "permission" }
            }
        };

    // Custom grant type for token exchange (RFC 8693)
    private const string TokenExchangeGrantType = "urn:ietf:params:oauth:grant-type:token-exchange";

    public static IEnumerable<Client> Clients =>
        new Client[]
        {
            // Machine-to-machine client (for internal services)
            new Client
            {
                ClientId = "m2m.internal",
                ClientName = "Internal Services",
                AllowedGrantTypes = GrantTypes.ClientCredentials,
                ClientSecrets = { new Secret("511536EF-F270-4058-80CA-1C89C192F69A".Sha256()) },
                AllowedScopes = { "talabat.api" }
            },

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
