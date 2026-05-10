using Duende.IdentityServer;
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

        // Add HttpContextAccessor
        builder.Services.AddHttpContextAccessor();

        // Application database (ASP.NET Identity only)
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

                // Disable automatic key management - we use AddDeveloperSigningCredential instead
                options.KeyManagement.Enabled = false;
            })
            // Clients, resources, scopes in memory (as requested)
            .AddInMemoryIdentityResources(Config.IdentityResources)
            .AddInMemoryApiResources(Config.ApiResources)
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
            // Custom profile service (two-phase: login without account, exchange to set account)
            .AddProfileService<CustomProfileService>()
            // Token exchange grant for account switching (RFC 8693)
            .AddExtensionGrantValidator<AccountSwitchTokenExchangeValidator>()
            // Development signing credential - creates persistent key in tempkey.jwk
            .AddDeveloperSigningCredential(persistKey: true, filename: "tempkey.jwk");

        // Configure authorization
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
            .AddLocalApi("IdentityServerAccessToken", options =>
            {
                options.ExpectedScope = "users.read";
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