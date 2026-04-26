using System.Reflection;
using IdentityServer.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace IdentityServer.OpenApi;

/// <summary>
/// Extension methods for configuring OpenAPI/Swagger with OAuth2 authentication.
/// </summary>
public static class OpenApiExtensions
{
    /// <summary>
    /// Adds OpenAPI/Swagger services with OAuth2 authentication configured for IdentityServer.
    /// </summary>
    public static IServiceCollection AddOpenApiWithAuth(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        var identityServerUrl = configuration["IdentityServer:Authority"] 
            ?? "https://localhost:5001";

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Identity Server Management API",
                Version = "v1",
                Description = "API for managing users, accounts, roles, and tenants in the identity system.",
                Contact = new OpenApiContact
                {
                    Name = "Platform Team",
                    Email = "platform@example.com"
                }
            });

            // Configure OAuth2 Authorization Code flow with PKCE
            options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OAuth2,
                Description = "OAuth2 Authorization Code flow with PKCE",
                Flows = new OpenApiOAuthFlows
                {
                    AuthorizationCode = new OpenApiOAuthFlow
                    {
                        AuthorizationUrl = new Uri($"{identityServerUrl}/connect/authorize"),
                        TokenUrl = new Uri($"{identityServerUrl}/connect/token"),
                        Scopes = new Dictionary<string, string>
                        {
                            { "openid", "OpenID Connect" },
                            { "profile", "User profile" },
                            { "email", "Email address" },
                            { "offline_access", "Refresh tokens" },
                            { "account", "Account information" },
                            { "talabat.api", "Talabat API access" }
                        }
                    }
                }
            });

            // Also support Bearer token (for testing with existing tokens)
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Enter your JWT token directly (without 'Bearer ' prefix)"
            });

            // Apply security requirement to operations with [Authorize] or [RequirePermission]
            options.OperationFilter<AuthorizeOperationFilter>();

            // Include XML comments if available
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }

            // Group by controller name
            options.TagActionsBy(api =>
            {
                if (api.GroupName != null)
                    return new[] { api.GroupName };

                if (api.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor controllerActionDescriptor)
                {
                    return new[] { controllerActionDescriptor.ControllerName };
                }

                return new[] { "Other" };
            });

            // Order tags
            options.OrderActionsBy(api => api.RelativePath);
        });

        return services;
    }

    /// <summary>
    /// Configures the OpenAPI/Swagger middleware.
    /// </summary>
    public static IApplicationBuilder UseOpenApiWithAuth(
        this IApplicationBuilder app, 
        IConfiguration configuration)
    {
        var identityServerUrl = configuration["IdentityServer:Authority"] 
            ?? "https://localhost:5001";

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Identity Server API v1");
            options.RoutePrefix = "swagger";

            // OAuth2 configuration for Swagger UI
            options.OAuthClientId("swagger.ui");
            options.OAuthAppName("Identity Server - Swagger UI");
            options.OAuthUsePkce();
            // Include offline_access scope to receive refresh tokens
            options.OAuthScopes("openid", "profile", "email", "offline_access", "account", "talabat.api");

            // UI customization
            options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
            options.DefaultModelsExpandDepth(-1); // Hide schemas section by default
            options.EnableDeepLinking();
            options.DisplayRequestDuration();
        });

        return app;
    }
}

/// <summary>
/// Operation filter that adds security requirements to endpoints with authorization attributes.
/// </summary>
public class AuthorizeOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Get authorization attributes from method and controller
        var methodAttributes = context.MethodInfo.GetCustomAttributes(true);
        var controllerAttributes = context.MethodInfo.DeclaringType?.GetCustomAttributes(true) ?? Array.Empty<object>();
        var allAttributes = methodAttributes.Concat(controllerAttributes);

        var hasAuthorize = allAttributes.OfType<AuthorizeAttribute>().Any();
        var hasRequirePermission = allAttributes.OfType<RequirePermissionAttribute>().Any();
        var hasAllowAnonymous = allAttributes.OfType<AllowAnonymousAttribute>().Any();

        if (hasAllowAnonymous)
        {
            return;
        }

        if (hasAuthorize || hasRequirePermission)
        {
            // Add 401 and 403 responses
            operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Unauthorized" });
            operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Forbidden" });

            // Add security requirement
            var oauth2Scheme = new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "oauth2"
                }
            };

            var bearerScheme = new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            };

            operation.Security = new List<OpenApiSecurityRequirement>
            {
                new OpenApiSecurityRequirement
                {
                    [oauth2Scheme] = new[] { "openid", "profile", "account", "platform.api", "shop.api" }
                },
                new OpenApiSecurityRequirement
                {
                    [bearerScheme] = Array.Empty<string>()
                }
            };

            // Add permission info to description if [RequirePermission] is used
            var requiredPermissions = allAttributes
                .OfType<RequirePermissionAttribute>()
                .Select(a => a.Policy?.Replace("Permission:", "") ?? "")
                .Where(p => !string.IsNullOrEmpty(p))
                .ToList();

            if (requiredPermissions.Any())
            {
                var permissionText = $"\n\n**Required Permission(s):** `{string.Join("`, `", requiredPermissions)}`";
                operation.Description = (operation.Description ?? "") + permissionText;
            }
        }
    }
}
