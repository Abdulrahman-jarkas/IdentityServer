using IdentityServer.Data;
using IdentityServer.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace IdentityServer;

/// <summary>
/// Seed data for the Identity Server.
/// Only seeds users - accounts and roles are managed by the Talabat API microservice.
/// </summary>
public static class SeedData
{
    /// <summary>
    /// Known User IDs for seeded users (deterministic for dev/test)
    /// </summary>
    public static class Users
    {
        public static readonly string Admin = "10000000-0000-0000-0000-000000000001";
        public static readonly string Owner = "10000000-0000-0000-0000-000000000002";
    }

    /// <summary>
    /// Seeds required data for ALL environments.
    /// </summary>
    public static void EnsureSeedData(WebApplication app)
    {
        using var scope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // Run migrations
        context.Database.Migrate();

        // Seed platform admin user
        SeedUser(userMgr, Users.Admin, "admin@talabat.com", "Platform", "Admin", "Admin123$");

        Log.Information("Required seed data completed successfully");
    }

    /// <summary>
    /// Seeds development-only test data.
    /// </summary>
    public static void EnsureDevSeedData(WebApplication app)
    {
        using var scope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        SeedUser(userMgr, Users.Owner, "owner@talabat.com", "Shop", "Owner", "Owner123$");

        Log.Information("Development seed data completed successfully");
    }

    private static ApplicationUser SeedUser(UserManager<ApplicationUser> userMgr, string id, string email, string firstName, string lastName, string password)
    {
        var user = userMgr.FindByIdAsync(id).Result;
        if (user == null)
        {
            user = new ApplicationUser
            {
                Id = id,
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = firstName,
                LastName = lastName,
                CreatedAt = DateTime.UtcNow
            };

            var result = userMgr.CreateAsync(user, password).Result;
            if (!result.Succeeded)
            {
                throw new Exception($"Failed to create user {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
            Log.Debug("User {Email} created with ID {Id}", email, id);
        }
        return user;
    }
}
