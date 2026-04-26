using IdentityServer.Api.Models;
using IdentityServer.Authorization;
using IdentityServer.Constants;
using IdentityServer.Data;
using IdentityServer.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Api.Controllers;

/// <summary>
/// Role management endpoints. Tenant-aware.
/// Returns roles scoped to the current tenant context.
/// </summary>
[Route("api/roles")]
public class RolesController : ApiControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<RolesController> _logger;

    public RolesController(
        ITenantContext tenantContext,
        ApplicationDbContext db,
        ILogger<RolesController> logger) : base(tenantContext)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Get roles based on current tenant context.
    /// Includes system roles (TenantId=null) + tenant-specific roles.
    /// </summary>
    [HttpGet]
    [RequirePermission(Permissions.Roles.Read)]
    public async Task<ActionResult<PagedResponse<RoleResponse>>> GetRoles([FromQuery] PagedRequest request)
    {
        var query = GetScopedRolesQuery();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.ToLower();
            query = query.Where(r =>
                r.Name.ToLower().Contains(search) ||
                (r.Description != null && r.Description.ToLower().Contains(search)));
        }

        var totalCount = await query.CountAsync();

        var roles = await query
            .OrderBy(r => r.IsSystem)
            .ThenBy(r => r.Name)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Include(r => r.AccountRoles)
            .ToListAsync();

        return Ok(new PagedResponse<RoleResponse>
        {
            Items = roles.Select(MapRole).ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }

    /// <summary>
    /// Get role by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.Roles.Read)]
    public async Task<ActionResult<RoleResponse>> GetRole(Guid id)
    {
        var role = await _db.AppRoles
            .Include(r => r.AccountRoles)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (role == null)
            return NotFoundError("Role not found.");

        if (!CanAccessRole(role))
            return NotFoundError("Role not found.");

        return Ok(MapRole(role));
    }

    /// <summary>
    /// Create a new role in current tenant context.
    /// </summary>
    [HttpPost]
    [RequirePermission(Permissions.Roles.Create)]
    public async Task<ActionResult<RoleResponse>> CreateRole([FromBody] CreateRoleRequest request)
    {
        var tenantType = CurrentTenantType;
        Guid? tenantId = null;

        if (tenantType == TenantType.System)
        {
            // System can create roles for any tenant type
            // TenantId from request determines if it's a template or specific tenant role
            tenantId = request.TenantId;
        }
        else if (tenantType == TenantType.Shop)
        {
            tenantId = CurrentTenantId;
        }
        else
        {
            return Forbidden("Cannot create roles from this context.");
        }

        // Check for duplicate
        var existing = await _db.AppRoles
            .AnyAsync(r => r.Name == request.Name &&
                           r.TenantType == tenantType &&
                           r.TenantId == tenantId);

        if (existing)
            return Conflict("A role with this name already exists.");

        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            TenantType = tenantType.Value,
            TenantId = tenantId,
            IsSystem = false,
            CreatedAt = DateTime.UtcNow,
            CreatedByAccountId = CurrentAccountId
        };

        role.SetPermissions(request.Permissions);

        _db.AppRoles.Add(role);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Role {RoleId} '{RoleName}' created by {CurrentAccountId}",
            role.Id, role.Name, CurrentAccountId);

        return CreatedAtAction(nameof(GetRole), new { id = role.Id }, MapRole(role));
    }

    /// <summary>
    /// Update a role.
    /// </summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.Roles.Update)]
    public async Task<ActionResult<RoleResponse>> UpdateRole(Guid id, [FromBody] UpdateRoleRequest request)
    {
        var role = await _db.AppRoles.FindAsync(id);
        if (role == null)
            return NotFoundError("Role not found.");

        if (!CanAccessRole(role))
            return NotFoundError("Role not found.");

        if (role.IsSystem)
            return BadRequestError("System roles cannot be modified.");

        // Check for duplicate name
        var duplicate = await _db.AppRoles
            .AnyAsync(r => r.Id != id &&
                           r.Name == request.Name &&
                           r.TenantType == role.TenantType &&
                           r.TenantId == role.TenantId);

        if (duplicate)
            return Conflict("A role with this name already exists.");

        role.Name = request.Name;
        role.Description = request.Description;
        role.SetPermissions(request.Permissions);

        await _db.SaveChangesAsync();

        _logger.LogInformation("Role {RoleId} '{RoleName}' updated by {CurrentAccountId}",
            role.Id, role.Name, CurrentAccountId);

        return Ok(MapRole(role));
    }

    /// <summary>
    /// Delete a role.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.Roles.Delete)]
    public async Task<IActionResult> DeleteRole(Guid id)
    {
        var role = await _db.AppRoles
            .Include(r => r.AccountRoles)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (role == null)
            return NotFoundError("Role not found.");

        if (!CanAccessRole(role))
            return NotFoundError("Role not found.");

        if (role.IsSystem)
            return BadRequestError("System roles cannot be deleted.");

        // Remove role assignments first
        _db.AccountRoles.RemoveRange(role.AccountRoles);
        _db.AppRoles.Remove(role);

        await _db.SaveChangesAsync();

        _logger.LogInformation("Role {RoleId} '{RoleName}' deleted by {CurrentAccountId}",
            id, role.Name, CurrentAccountId);

        return NoContent();
    }

    // ============================================
    // Helper Methods
    // ============================================

    private IQueryable<Role> GetScopedRolesQuery()
    {
        // Scope query based on tenant type
        return CurrentTenantType switch
        {
            TenantType.System => _db.AppRoles, // System sees all
            TenantType.Shop => _db.AppRoles.Where(r =>
                r.TenantType == TenantType.Shop &&
                (r.TenantId == CurrentTenantId || r.TenantId == null)), // Shop roles + templates
            TenantType.Warehouse => _db.AppRoles.Where(r =>
                r.TenantType == TenantType.Warehouse &&
                (r.TenantId == CurrentTenantId || r.TenantId == null)),
            _ => _db.AppRoles.Where(r => false) // No access
        };
    }

    private bool CanAccessRole(Role role)
    {
        if (CurrentTenantType == TenantType.System)
            return true;

        // Can access roles for current tenant type + (specific tenant or template)
        return role.TenantType == CurrentTenantType &&
               (role.TenantId == CurrentTenantId || role.TenantId == null);
    }

    private static RoleResponse MapRole(Role role)
    {
        return new RoleResponse
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            TenantType = role.TenantType,
            TenantId = role.TenantId,
            IsSystem = role.IsSystem,
            CreatedAt = role.CreatedAt,
            Permissions = role.GetPermissions(),
            AccountCount = role.AccountRoles?.Count ?? 0
        };
    }
}
