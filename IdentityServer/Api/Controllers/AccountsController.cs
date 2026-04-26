using IdentityServer.Api.Models;
using IdentityServer.Authorization;
using IdentityServer.Constants;
using IdentityServer.Data;
using IdentityServer.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Api.Controllers;

/// <summary>
/// Account management endpoints. Tenant-aware.
/// Access is controlled by accounts.* permissions.
/// </summary>
[Route("api/accounts")]
public class AccountsController : ApiControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<AccountsController> _logger;

    public AccountsController(
        ITenantContext tenantContext,
        ApplicationDbContext db,
        ILogger<AccountsController> logger) : base(tenantContext)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Get accounts based on current tenant context.
    /// </summary>
    [HttpGet]
    [RequirePermission(Permissions.Accounts.Read)]
    public async Task<ActionResult<PagedResponse<AccountResponse>>> GetAccounts([FromQuery] PagedRequest request)
    {
        // Determine which accounts to return based on current account's tenant
        var query = GetScopedAccountsQuery();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.ToLower();
            query = query.Where(a =>
                (a.DisplayName != null && a.DisplayName.ToLower().Contains(search)) ||
                (a.User != null && a.User.Email != null && a.User.Email.ToLower().Contains(search)));
        }

        var totalCount = await query.CountAsync();

        var accounts = await query
            .OrderBy(a => a.DisplayName)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Include(a => a.User)
            .Include(a => a.AccountRoles)
                .ThenInclude(ar => ar.Role)
            .ToListAsync();

        return Ok(new PagedResponse<AccountResponse>
        {
            Items = accounts.Select(MapAccount).ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }

    /// <summary>
    /// Get account by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.Accounts.Read)]
    public async Task<ActionResult<AccountResponse>> GetAccount(Guid id)
    {
        var account = await _db.Accounts
            .Include(a => a.User)
            .Include(a => a.AccountRoles)
                .ThenInclude(ar => ar.Role)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (account == null)
            return NotFoundError("Account not found.");

        // Check access based on tenant scope
        if (!CanAccessAccount(account))
            return NotFoundError("Account not found.");

        return Ok(MapAccount(account));
    }

    /// <summary>
    /// Create a new account for a user in current tenant context.
    /// </summary>
    [HttpPost]
    [RequirePermission(Permissions.Accounts.Create)]
    public async Task<ActionResult<AccountResponse>> CreateAccount([FromBody] CreateAccountRequest request)
    {
        // Determine tenant type and scope based on current context
        var tenantType = CurrentTenantType;
        Guid? tenantId = null;

        if (tenantType == TenantType.System)
        {
            // System creates system accounts
            tenantType = TenantType.System;
            tenantId = null;
        }
        else if (tenantType == TenantType.Shop)
        {
            tenantId = CurrentTenantId;
        }
        else
        {
            return Forbidden("Cannot create accounts from this context.");
        }

        // Check user exists
        var user = await _db.Users.FindAsync(request.UserId);
        if (user == null)
            return NotFoundError("User not found.");

        // Check for existing account
        var existing = await _db.Accounts
            .FirstOrDefaultAsync(a => a.UserId == request.UserId &&
                                      a.TenantType == tenantType &&
                                      a.TenantId == tenantId);

        if (existing != null)
        {
            if (existing.Status == AccountStatus.Deleted)
            {
                // Reactivate
                existing.Status = AccountStatus.Active;
                existing.DisplayName = request.DisplayName ?? existing.DisplayName;
                existing.RefreshSecurityStamp(); // Invalidate old tokens
                await _db.SaveChangesAsync();

                _logger.LogInformation("Account {AccountId} reactivated by {CurrentAccountId}",
                    existing.Id, CurrentAccountId);

                return Ok(await LoadAccount(existing.Id));
            }
            return Conflict("Account already exists for this user in this tenant.");
        }

        // Create account
        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            TenantType = tenantType.Value,
            TenantId = tenantId,
            DisplayName = request.DisplayName ?? user.FullName,
            Status = AccountStatus.Active,
            CreatedAt = DateTime.UtcNow,
            CreatedByAccountId = CurrentAccountId
        };

        _db.Accounts.Add(account);

        // Validate and assign roles
        var invalidRoleIds = new List<Guid>();
        foreach (var roleId in request.RoleIds.Distinct())
        {
            var role = await _db.AppRoles
                .FirstOrDefaultAsync(r => r.Id == roleId &&
                                          r.TenantType == tenantType &&
                                          (r.TenantId == tenantId || r.TenantId == null));

            if (role == null)
            {
                invalidRoleIds.Add(roleId);
            }
            else
            {
                _db.AccountRoles.Add(new AccountRole
                {
                    Id = Guid.NewGuid(),
                    AccountId = account.Id,
                    RoleId = role.Id,
                    AssignedAt = DateTime.UtcNow,
                    AssignedByAccountId = CurrentAccountId
                });
            }
        }

        if (invalidRoleIds.Any())
        {
            return BadRequestError($"Invalid role IDs: {string.Join(", ", invalidRoleIds)}");
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("Account {AccountId} created for user {UserId} by {CurrentAccountId}",
            account.Id, request.UserId, CurrentAccountId);

        return CreatedAtAction(nameof(GetAccount), new { id = account.Id }, await LoadAccount(account.Id));
    }

    /// <summary>
    /// Update an account.
    /// </summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.Accounts.Update)]
    public async Task<ActionResult<AccountResponse>> UpdateAccount(Guid id, [FromBody] UpdateAccountRequest request)
    {
        var account = await _db.Accounts
            .Include(a => a.AccountRoles)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (account == null)
            return NotFoundError("Account not found.");

        if (!CanAccessAccount(account))
            return NotFoundError("Account not found.");

        // Update display name
        if (request.DisplayName != null)
            account.DisplayName = request.DisplayName;

        // Update roles if provided
        if (request.RoleIds != null)
        {
            // Validate all role IDs first
            var invalidRoleIds = new List<Guid>();
            var validRoles = new List<Role>();

            foreach (var roleId in request.RoleIds.Distinct())
            {
                var role = await _db.AppRoles
                    .FirstOrDefaultAsync(r => r.Id == roleId &&
                                              r.TenantType == account.TenantType &&
                                              (r.TenantId == account.TenantId || r.TenantId == null));

                if (role == null)
                {
                    invalidRoleIds.Add(roleId);
                }
                else
                {
                    validRoles.Add(role);
                }
            }

            if (invalidRoleIds.Any())
            {
                return BadRequestError($"Invalid role IDs: {string.Join(", ", invalidRoleIds)}");
            }

            // Remove existing roles and add new ones
            _db.AccountRoles.RemoveRange(account.AccountRoles);

            foreach (var role in validRoles)
            {
                _db.AccountRoles.Add(new AccountRole
                {
                    Id = Guid.NewGuid(),
                    AccountId = account.Id,
                    RoleId = role.Id,
                    AssignedAt = DateTime.UtcNow,
                    AssignedByAccountId = CurrentAccountId
                });
            }

            // Refresh security stamp when roles change
            account.RefreshSecurityStamp();
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("Account {AccountId} updated by {CurrentAccountId}", id, CurrentAccountId);

        return Ok(await LoadAccount(id));
    }

    /// <summary>
    /// Delete (soft) an account.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.Accounts.Delete)]
    public async Task<IActionResult> DeleteAccount(Guid id)
    {
        var account = await _db.Accounts.FindAsync(id);
        if (account == null)
            return NotFoundError("Account not found.");

        if (!CanAccessAccount(account))
            return NotFoundError("Account not found.");

        // Cannot delete own account
        if (account.Id == CurrentAccountId)
            return BadRequestError("Cannot delete your own account.");

        // Cannot delete customer account
        if (account.TenantType == TenantType.Customer)
            return BadRequestError("Cannot delete customer accounts. Delete the user instead.");

        account.Status = AccountStatus.Deleted;
        account.RefreshSecurityStamp(); // Invalidate tokens for this account
        await _db.SaveChangesAsync();

        _logger.LogInformation("Account {AccountId} deleted by {CurrentAccountId}", id, CurrentAccountId);

        return NoContent();
    }

    // ============================================
    // Helper Methods
    // ============================================

    private IQueryable<Account> GetScopedAccountsQuery()
    {
        var baseQuery = _db.Accounts.Where(a => a.Status != AccountStatus.Deleted);

        // Scope query based on tenant type
        return CurrentTenantType switch
        {
            TenantType.System => baseQuery, // System sees all
            TenantType.Shop => baseQuery.Where(a => a.TenantType == TenantType.Shop && a.TenantId == CurrentTenantId),
            TenantType.Warehouse => baseQuery.Where(a => a.TenantType == TenantType.Warehouse && a.TenantId == CurrentTenantId),
            _ => baseQuery.Where(a => false) // No access
        };
    }

    private bool CanAccessAccount(Account account)
    {
        if (CurrentTenantType == TenantType.System)
            return true;

        return account.TenantType == CurrentTenantType && account.TenantId == CurrentTenantId;
    }

    private async Task<AccountResponse> LoadAccount(Guid id)
    {
        var account = await _db.Accounts
            .Include(a => a.User)
            .Include(a => a.AccountRoles)
                .ThenInclude(ar => ar.Role)
            .FirstAsync(a => a.Id == id);

        return MapAccount(account);
    }

    private static AccountResponse MapAccount(Account account)
    {
        var permissions = account.AccountRoles
            .Where(ar => ar.Role != null)
            .SelectMany(ar => ar.Role!.GetPermissions())
            .Distinct()
            .ToList();

        return new AccountResponse
        {
            Id = account.Id,
            UserId = account.UserId,
            UserEmail = account.User?.Email,
            UserFullName = account.User?.FullName,
            TenantType = account.TenantType,
            TenantId = account.TenantId,
            DisplayName = account.DisplayName,
            Status = account.Status,
            CreatedAt = account.CreatedAt,
            Roles = account.AccountRoles
                .Where(ar => ar.Role != null)
                .Select(ar => new RoleSummary { Id = ar.RoleId, Name = ar.Role!.Name })
                .ToList(),
            Permissions = permissions
        };
    }
}
