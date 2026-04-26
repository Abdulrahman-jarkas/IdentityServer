using IdentityServer.Constants;
using IdentityServer.Data;
using IdentityServer.Data.Entities;
using IdentityServer.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace IdentityServer;

/// <summary>
/// Seed data for the Identity Server.
/// - EnsureSeedData: Seeds required data for all environments (roles + platform admin)
/// - EnsureDevSeedData: Seeds additional test data for development only
/// </summary>
public static class SeedData
{
    /// <summary>
    /// Known Shop IDs from the Shop microservice (for dev/test purposes)
    /// </summary>
    public static class Shops
    {
        public static readonly Guid AlBaik = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        public static readonly Guid ShawarmaHouse = Guid.Parse("b2c3d4e5-f6a7-8901-bcde-f12345678901");
    }

    /// <summary>
    /// Seeds required data for ALL environments.
    /// This includes system roles and platform admin account.
    /// </summary>
    public static void EnsureSeedData(WebApplication app)
    {
        using var scope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // Run migrations
        context.Database.Migrate();

        // Seed all system roles (required for all environments)
        var roles = SeedSystemRoles(context);

        // Seed platform admin (required for all environments)
        SeedPlatformAdmin(context, userMgr, roles.systemAdmin);

        Log.Information("Required seed data completed successfully");
    }

    /// <summary>
    /// Seeds development-only test data.
    /// Call this after EnsureSeedData for dev environments.
    /// </summary>
    public static void EnsureDevSeedData(WebApplication app)
    {
        using var scope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // Get existing roles
        var systemAdminRole = context.AppRoles.First(r => r.Name == "SystemAdmin" && r.TenantType == TenantType.System);
        var customerRole = context.AppRoles.First(r => r.Name == "Customer" && r.TenantType == TenantType.Customer);
        var shopOwnerRole = context.AppRoles.First(r => r.Name == "ShopOwner" && r.TenantType == TenantType.Shop);
        var shopAdminRole = context.AppRoles.First(r => r.Name == "ShopAdmin" && r.TenantType == TenantType.Shop);
        var shopStaffRole = context.AppRoles.First(r => r.Name == "ShopStaff" && r.TenantType == TenantType.Shop);

        // ============================================
        // User 1: Platform Admin (admin@talabat.com)
        // - System account as Platform Admin
        // - Shop account at AlBaik as ShopAdmin
        // - Shop account at ShawarmaHouse as ShopOwner
        // - Customer account
        // ============================================
        var admin = SeedUser(userMgr, "admin@talabat.com", "Platform", "Admin", "Admin123$");

        // System account (already created in EnsureSeedData, but ensure shop accounts exist)
        SeedAccount(context, admin.Id, TenantType.Shop, Shops.AlBaik, "Admin @ AlBaik", shopAdminRole.Id);
        SeedAccount(context, admin.Id, TenantType.Shop, Shops.ShawarmaHouse, "Admin @ ShawarmaHouse", shopOwnerRole.Id);
        SeedAccount(context, admin.Id, TenantType.Customer, null, "Admin Customer", customerRole.Id);

        // ============================================
        // User 2: Shop Owner (owner@talabat.com)
        // - Customer account
        // - Shop account at AlBaik as ShopOwner
        // - Shop account at ShawarmaHouse as ShopAdmin
        // ============================================
        var owner = SeedUser(userMgr, "owner@talabat.com", "Shop", "Owner", "Owner123$");
        SeedAccount(context, owner.Id, TenantType.Customer, null, "Shop Owner", customerRole.Id);
        SeedAccount(context, owner.Id, TenantType.Shop, Shops.AlBaik, "Owner @ AlBaik", shopOwnerRole.Id);
        SeedAccount(context, owner.Id, TenantType.Shop, Shops.ShawarmaHouse, "Admin @ ShawarmaHouse", shopAdminRole.Id);

        // ============================================
        // User 3: Staff (staff@talabat.com)
        // - Customer account
        // - Shop account at AlBaik as ShopStaff
        // ============================================
        var staff = SeedUser(userMgr, "staff@talabat.com", "Shop", "Staff", "Staff123$");
        SeedAccount(context, staff.Id, TenantType.Customer, null, "Staff Customer", customerRole.Id);
        SeedAccount(context, staff.Id, TenantType.Shop, Shops.AlBaik, "Staff @ AlBaik", shopStaffRole.Id);

        Log.Information("Development seed data completed successfully");
    }

    /// <summary>
    /// Seeds the platform admin user and account.
    /// </summary>
    private static void SeedPlatformAdmin(ApplicationDbContext context, UserManager<ApplicationUser> userMgr, Role systemAdminRole)
    {
        var admin = SeedUser(userMgr, "admin@talabat.com", "Platform", "Admin", "Admin123$");
        SeedAccount(context, admin.Id, TenantType.System, null, "Platform Admin", systemAdminRole.Id);
    }

    /// <summary>
    /// Seeds all system roles for all tenant types.
    /// </summary>
    private static (Role systemAdmin, Role customer, Role shopOwner, Role shopAdmin, Role shopStaff) SeedSystemRoles(ApplicationDbContext context)
    {
        var systemAdmin = SeedRole(context, "SystemAdmin", "Full system administrator access", TenantType.System, null, new[]
        {
            Permissions.Users.Read,
            Permissions.Users.Create,
            Permissions.Users.Update,
            Permissions.Users.Delete,
            Permissions.Users.Lock,
            Permissions.Accounts.Read,
            Permissions.Accounts.Create,
            Permissions.Accounts.Update,
            Permissions.Accounts.Delete,
            Permissions.Roles.Read,
            Permissions.Roles.Create,
            Permissions.Roles.Update,
            Permissions.Roles.Delete,
            Permissions.Analytics.Read
        });

        var customer = SeedRole(context, "Customer", "Standard customer", TenantType.Customer, null, new[]
        {
            Permissions.Profile.Read,
            Permissions.Profile.Update,
            Permissions.Orders.Read,
            Permissions.Orders.Create,
            Permissions.Orders.Cancel,
            Permissions.Addresses.Manage,
            Permissions.Favorites.Manage
        });

        var shopOwner = SeedRole(context, "ShopOwner", "Shop owner with full access", TenantType.Shop, null, new[]
        {
            Permissions.Accounts.Read,
            Permissions.Accounts.Create,
            Permissions.Accounts.Update,
            Permissions.Accounts.Delete,
            Permissions.Roles.Read,
            Permissions.Roles.Create,
            Permissions.Roles.Update,
            Permissions.Roles.Delete,
            Permissions.Orders.Read,
            Permissions.Orders.Create,
            Permissions.Orders.Update,
            Permissions.Orders.Cancel,
            Permissions.Menu.Read,
            Permissions.Menu.Manage,
            Permissions.Reports.Read
        });

        var shopAdmin = SeedRole(context, "ShopAdmin", "Shop administrator", TenantType.Shop, null, new[]
        {
            Permissions.Accounts.Read,
            Permissions.Orders.Read,
            Permissions.Orders.Create,
            Permissions.Orders.Update,
            Permissions.Menu.Read,
            Permissions.Menu.Manage,
            Permissions.Reports.Read
        });

        var shopStaff = SeedRole(context, "ShopStaff", "Shop staff member", TenantType.Shop, null, new[]
        {
            Permissions.Orders.Read,
            Permissions.Orders.Update,
            Permissions.Menu.Read
        });

        return (systemAdmin, customer, shopOwner, shopAdmin, shopStaff);
    }

    private static Role SeedRole(ApplicationDbContext context, string name, string description, TenantType tenantType, Guid? tenantId, string[] permissions)
    {
        var role = context.AppRoles.FirstOrDefault(r => r.Name == name && r.TenantType == tenantType && r.TenantId == tenantId);
        if (role == null)
        {
            role = new Role
            {
                Id = Guid.NewGuid(),
                Name = name,
                Description = description,
                TenantType = tenantType,
                TenantId = tenantId,
                IsSystem = true,
                CreatedAt = DateTime.UtcNow
            };
            role.SetPermissions(permissions);
            context.AppRoles.Add(role);
            context.SaveChanges();
            Log.Debug("Role {RoleName} created for {TenantType}", name, tenantType);
        }
        return role;
    }

    private static ApplicationUser SeedUser(UserManager<ApplicationUser> userMgr, string email, string firstName, string lastName, string password)
    {
        var user = userMgr.FindByEmailAsync(email).Result;
        if (user == null)
        {
            user = new ApplicationUser
            {
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
            Log.Debug("User {Email} created", email);
        }
        return user;
    }

    private static Account SeedAccount(ApplicationDbContext context, string userId, TenantType tenantType, Guid? tenantId, string displayName, Guid roleId)
    {
        var account = context.Accounts.FirstOrDefault(a => a.UserId == userId && a.TenantType == tenantType && a.TenantId == tenantId);
        if (account == null)
        {
            account = new Account
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TenantType = tenantType,
                TenantId = tenantId,
                DisplayName = displayName,
                Status = AccountStatus.Active,
                CreatedAt = DateTime.UtcNow
            };
            context.Accounts.Add(account);
            context.SaveChanges();

            context.AccountRoles.Add(new AccountRole
            {
                Id = Guid.NewGuid(),
                AccountId = account.Id,
                RoleId = roleId,
                AssignedAt = DateTime.UtcNow
            });
            context.SaveChanges();

            Log.Debug("Account {DisplayName} created for user {UserId} as {TenantType}", displayName, userId, tenantType);
        }
        return account;
    }
}
