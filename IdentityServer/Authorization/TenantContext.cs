using System.Security.Claims;
using IdentityServer.Data.Entities;

namespace IdentityServer.Authorization;

/// <summary>
/// Provides access to the current user's tenant context from claims.
/// </summary>
public class TenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly Lazy<HashSet<string>> _permissions;

    public TenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
        _permissions = new Lazy<HashSet<string>>(() => LoadPermissions());
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public Guid? CurrentAccountId
    {
        get
        {
            var accountIdClaim = User?.FindFirst("account_id")?.Value;
            return Guid.TryParse(accountIdClaim, out var accountId) ? accountId : null;
        }
    }

    public TenantType? CurrentTenantType
    {
        get
        {
            var tenantTypeClaim = User?.FindFirst("tenant_type")?.Value;
            return Enum.TryParse<TenantType>(tenantTypeClaim, true, out var tenantType) 
                ? tenantType 
                : null;
        }
    }

    public Guid? CurrentTenantId
    {
        get
        {
            var tenantIdClaim = User?.FindFirst("tenant_id")?.Value;
            return Guid.TryParse(tenantIdClaim, out var tenantId) ? tenantId : null;
        }
    }

    public string? CurrentUserId => User?.FindFirst("sub")?.Value 
        ?? User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    public bool IsSystemAccount => CurrentTenantType == TenantType.System;
    public bool IsCustomerAccount => CurrentTenantType == TenantType.Customer;
    public bool IsShopAccount => CurrentTenantType == TenantType.Shop;
    public bool IsWarehouseAccount => CurrentTenantType == TenantType.Warehouse;

    public IReadOnlySet<string> Permissions => _permissions.Value;

    public bool HasPermission(string permission)
    {
        if (string.IsNullOrEmpty(permission))
            return false;

        return Permissions.Contains(permission);
    }

    /// <summary>
    /// Check if user can access data for a specific tenant.
    /// System accounts can access any tenant.
    /// Other accounts can only access their own tenant.
    /// </summary>
    public bool CanAccessTenant(TenantType tenantType, Guid? tenantId)
    {
        // System accounts can access everything
        if (IsSystemAccount)
            return true;

        // Must match tenant type and tenant id
        return CurrentTenantType == tenantType && CurrentTenantId == tenantId;
    }

    private HashSet<string> LoadPermissions()
    {
        if (User == null)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return User.FindAll("permission")
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
