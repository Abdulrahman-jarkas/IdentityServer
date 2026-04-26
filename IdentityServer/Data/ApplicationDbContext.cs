using IdentityServer.Data.Entities;
using IdentityServer.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Role> AppRoles => Set<Role>();
    public DbSet<AccountRole> AccountRoles => Set<AccountRole>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ConfigureApplicationUser(builder);
        ConfigureAccount(builder);
        ConfigureRole(builder);
        ConfigureAccountRole(builder);
    }

    private static void ConfigureApplicationUser(ModelBuilder builder)
    {
        builder.Entity<ApplicationUser>(entity =>
        {
            // Ignore computed properties
            entity.Ignore(e => e.FullName);
            entity.Ignore(e => e.CustomerAccount);
            entity.Ignore(e => e.SystemAccount);
            entity.Ignore(e => e.ShopAccounts);
        });
    }

    private static void ConfigureAccount(ModelBuilder builder)
    {
        builder.Entity<Account>(entity =>
        {
            entity.ToTable("Accounts");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.DisplayName).HasMaxLength(200);
            entity.Property(e => e.TenantType).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);

            // User relationship
            entity.HasOne(e => e.User)
                .WithMany(u => u.Accounts)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Self-referencing (who created this account)
            entity.HasOne(e => e.CreatedByAccount)
                .WithMany(e => e.CreatedAccounts)
                .HasForeignKey(e => e.CreatedByAccountId)
                .OnDelete(DeleteBehavior.NoAction);

            // Unique constraint: one account per user per tenant type per tenant id
            // For Customer/System: only one per user (TenantId is null)
            entity.HasIndex(e => new { e.UserId, e.TenantType, e.TenantId }).IsUnique();

            // Ignore computed properties
            entity.Ignore(e => e.IsCustomerAccount);
            entity.Ignore(e => e.IsSystemAccount);
        });
    }

    private static void ConfigureRole(ModelBuilder builder)
    {
        builder.Entity<Role>(entity =>
        {
            entity.ToTable("AppRoles"); // Avoid conflict with ASP.NET Identity Roles
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.TenantType).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.Permissions).HasMaxLength(4000); // JSON array

            entity.HasOne(e => e.CreatedByAccount)
                .WithMany()
                .HasForeignKey(e => e.CreatedByAccountId)
                .OnDelete(DeleteBehavior.NoAction);

            // Unique role name per tenant type + tenant id
            entity.HasIndex(e => new { e.TenantType, e.TenantId, e.Name }).IsUnique();
        });
    }

    private static void ConfigureAccountRole(ModelBuilder builder)
    {
        builder.Entity<AccountRole>(entity =>
        {
            entity.ToTable("AccountRoles");
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Account)
                .WithMany(a => a.AccountRoles)
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Role)
                .WithMany(r => r.AccountRoles)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.AssignedByAccount)
                .WithMany()
                .HasForeignKey(e => e.AssignedByAccountId)
                .OnDelete(DeleteBehavior.NoAction);

            // Unique role per account
            entity.HasIndex(e => new { e.AccountId, e.RoleId }).IsUnique();
        });
    }
}
