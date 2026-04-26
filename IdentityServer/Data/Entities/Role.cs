using System.Text.Json;

namespace IdentityServer.Data.Entities;

/// <summary>
/// Role defines a set of permissions that can be assigned to accounts.
/// Roles are scoped by TenantType and optionally by specific TenantId.
/// </summary>
public class Role
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }

    /// <summary>
    /// Which tenant type this role applies to.
    /// </summary>
    public TenantType TenantType { get; set; }

    /// <summary>
    /// null = System/template role for all tenants of this type.
    /// Guid = Custom role for a specific tenant.
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// System roles cannot be modified or deleted.
    /// </summary>
    public bool IsSystem { get; set; }

    /// <summary>
    /// Permissions stored as JSON array: ["orders.read", "orders.write"]
    /// </summary>
    public string Permissions { get; set; } = "[]";

    // Audit
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedByAccountId { get; set; }
    public Account? CreatedByAccount { get; set; }

    // Navigation
    public ICollection<AccountRole> AccountRoles { get; set; } = new List<AccountRole>();

    // Helper methods
    public List<string> GetPermissions()
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(Permissions) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    public void SetPermissions(IEnumerable<string> permissions)
    {
        Permissions = JsonSerializer.Serialize(permissions.Distinct().OrderBy(p => p).ToList());
    }

    public bool HasPermission(string permission)
    {
        return GetPermissions().Contains(permission, StringComparer.OrdinalIgnoreCase);
    }
}
