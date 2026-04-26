using Duende.IdentityServer;
using IdentityServer.Authorization;
using IdentityServer.Authorization.Handlers;
using IdentityServer.Data;
using IdentityServer.Models;
using IdentityServer.OpenApi;
using IdentityServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace IdentityServer;

internal static class HostingExtensions
{
    public static WebApplication ConfigureServices(this WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        builder.Services.AddRazorPages();

        // Add API Controllers
        builder.Services.AddControllers();

        // Add OpenAPI/Swagger with OAuth2 authentication
        builder.Services.AddOpenApiWithAuth(builder.Configuration);

        // Add HttpContextAccessor for TenantContext
        builder.Services.AddHttpContextAccessor();

        // Application database (ASP.NET Identity + custom entities)
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));

        // ASP.NET Identity with custom claims factory
        builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders()
            .AddClaimsPrincipalFactory<CustomUserClaimsPrincipalFactory>();

        // IdentityServer
        builder.Services
            .AddIdentityServer(options =>
            {
                options.Events.RaiseErrorEvents = true;
                options.Events.RaiseInformationEvents = true;
                options.Events.RaiseFailureEvents = true;
                options.Events.RaiseSuccessEvents = true;

                options.EmitStaticAudienceClaim = true;

                // Disable automatic key management (use your own certificate in production)
                options.KeyManagement.Enabled = false;
            })
            // Clients, resources, scopes in memory (as requested)
            .AddInMemoryIdentityResources(Config.IdentityResources)
            .AddInMemoryApiScopes(Config.ApiScopes)
            .AddInMemoryClients(Config.Clients)
            // ASP.NET Identity integration
            .AddAspNetIdentity<ApplicationUser>()
            // Operational store for persisted grants (refresh tokens, etc.)
            .AddOperationalStore(options =>
            {
                options.ConfigureDbContext = b =>
                    b.UseSqlServer(connectionString, sql =>
                        sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));

                // Automatically clean up expired tokens
                options.EnableTokenCleanup = true;
                options.TokenCleanupInterval = 3600; // 1 hour
            })
            // Custom profile service to add account/role/permission claims
            .AddProfileService<CustomProfileService>()
            // Token exchange grant for account switching (RFC 8693)
            // Delegates token validation to Duende's ITokenValidator
            .AddExtensionGrantValidator<AccountSwitchTokenExchangeValidator>()
            // Developer signing credential (replace with real certificate in production)
            .AddDeveloperSigningCredential();

        // Register custom services
        builder.Services.AddScoped<IAccountService, AccountService>();

        // Register authorization services
        builder.Services.AddScoped<ITenantContext, TenantContext>();
        builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        // Register dynamic permission policy provider for [RequirePermission] attribute
        builder.Services.AddPermissionPolicies();

        // Configure authorization - default policy requires authenticated user
        builder.Services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        // External authentication (Google) and local API authentication
        builder.Services.AddAuthentication()
            .AddGoogle(options =>
            {
                options.SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;
                options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? "not-configured";
                options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? "not-configured";
            })
            // Add local API authentication for Bearer token validation
            // Uses Duende's internal ITokenValidator for efficient, secure token validation
            .AddLocalApi(IdentityServerConstants.LocalApi.AuthenticationScheme, options =>
            {
                options.ExpectedScope = "openid";
                // Reduce clock skew from default 5 minutes
                // Set to TimeSpan.Zero for exact expiration (testing only)
            });

        return builder.Build();
    }

    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        app.UseSerilogRequestLogging();

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();

            // Enable OpenAPI/Swagger in development
            app.UseOpenApiWithAuth(app.Configuration);
        }

        app.UseStaticFiles();
        app.UseRouting();
        app.UseIdentityServer();
        app.UseAuthorization();

        // Map API controllers
        app.MapControllers();

        app.MapRazorPages()
            .RequireAuthorization();

        return app;
    }
}