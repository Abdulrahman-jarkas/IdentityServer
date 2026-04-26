using IdentityServer.Models;

namespace IdentityServer.Data.Entities;

/// <summary>
/// Represents a user's authorization context within a specific tenant type.
/// A user can have multiple accounts (e.g., Customer + ShopStaff + SystemAdmin).
/// </summary>
public class Account
{
    public Guid Id { get; set; }

    // User relationship (ASP.NET Identity)
    public required string UserId { get; set; }
    public ApplicationUser? User { get; set; }

    // Tenant context
    public TenantType TenantType { get; set; }

    /// <summary>
    /// External tenant ID from other microservices.
    /// null for Customer and System accounts.
    /// Guid for Shop, Warehouse, Fleet accounts (ID from respective service).
    /// </summary>
    public Guid? TenantId { get; set; }

    // Account details
    public string? DisplayName { get; set; }
    public AccountStatus Status { get; set; } = AccountStatus.Active;

    /// <summary>
    /// Security stamp that changes when account security-related data changes:
    /// - Status changes (suspended, deleted)
    /// - Roles added/removed
    /// - Permissions changed
    /// Used to invalidate tokens when account security context changes.
    /// </summary>
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString();

    // Audit
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedByAccountId { get; set; }
    public Account? CreatedByAccount { get; set; }

    // Navigation
    public ICollection<AccountRole> AccountRoles { get; set; } = new List<AccountRole>();
    public ICollection<Account> CreatedAccounts { get; set; } = new List<Account>();

    // Computed properties
    public bool IsCustomerAccount => TenantType == TenantType.Customer;
    public bool IsSystemAccount => TenantType == TenantType.System;

    /// <summary>
    /// Regenerates the security stamp. Call this when security-related data changes.
    /// </summary>
    public void RefreshSecurityStamp()
    {
        SecurityStamp = Guid.NewGuid().ToString();
        UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Type of tenant/context for the account.
/// </summary>
public enum TenantType
{
    /// <summary>End customer - orders food. TenantId = null.</summary>
    Customer = 0,

    /// <summary>Platform staff/admin. TenantId = null.</summary>
    System = 1,

    /// <summary>Shop/Restaurant staff. TenantId = ShopId from Shop Service.</summary>
    Shop = 2,

    /// <summary>Warehouse staff. TenantId = WarehouseId from Warehouse Service.</summary>
    Warehouse = 3
}

public enum AccountStatus
{
    Active = 0,
    Suspended = 1,
    Invited = 2,
    Deleted = 3
}
