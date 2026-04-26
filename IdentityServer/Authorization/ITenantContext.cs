using IdentityServer.Data.Entities;

namespace IdentityServer.Authorization;

/// <summary>
/// Represents the current tenant context for the request.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// The current account ID.
    /// </summary>
    Guid? CurrentAccountId { get; }

    /// <summary>
    /// The tenant type of the current account.
    /// </summary>
    TenantType? CurrentTenantType { get; }

    /// <summary>
    /// The tenant ID (null for Customer/System accounts).
    /// </summary>
    Guid? CurrentTenantId { get; }

    /// <summary>
    /// The current user ID (ASP.NET Identity User ID).
    /// </summary>
    string? CurrentUserId { get; }

    /// <summary>
    /// Whether the current account is a System account.
    /// </summary>
    bool IsSystemAccount { get; }

    /// <summary>
    /// Whether the current account is a Customer account.
    /// </summary>
    bool IsCustomerAccount { get; }

    /// <summary>
    /// Whether the current account is a Shop account.
    /// </summary>
    bool IsShopAccount { get; }

    /// <summary>
    /// Whether the current account is a Warehouse account.
    /// </summary>
    bool IsWarehouseAccount { get; }

    /// <summary>
    /// The permissions of the current account.
    /// </summary>
    IReadOnlySet<string> Permissions { get; }

    /// <summary>
    /// Checks if the current user has the specified permission.
    /// </summary>
    bool HasPermission(string permission);

    /// <summary>
    /// Checks if the current user can access the specified tenant.
    /// </summary>
    bool CanAccessTenant(TenantType tenantType, Guid? tenantId);
}
