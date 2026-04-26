using IdentityServer.Api.Models;
using IdentityServer.Authorization;
using IdentityServer.Constants;
using IdentityServer.Data;
using IdentityServer.Data.Entities;
using IdentityServer.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Api.Controllers;

/// <summary>
/// User management endpoints. Only accessible by System accounts.
/// When creating a user, a Customer account is automatically created.
/// </summary>
[Route("api/users")]
public class UsersController : ApiControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        ITenantContext tenantContext,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        ILogger<UsersController> logger) : base(tenantContext)
    {
        _userManager = userManager;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Get all users. System only.
    /// </summary>
    [HttpGet]
    [RequirePermission(Permissions.Users.Read)]
    public async Task<ActionResult<PagedResponse<UserResponse>>> GetUsers([FromQuery] PagedRequest request)
    {
        var query = _db.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.ToLower();
            query = query.Where(u =>
                (u.Email != null && u.Email.ToLower().Contains(search)) ||
                (u.FirstName != null && u.FirstName.ToLower().Contains(search)) ||
                (u.LastName != null && u.LastName.ToLower().Contains(search)));
        }

        var totalCount = await query.CountAsync();

        var users = await query
            .OrderBy(u => u.Email)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Include(u => u.Accounts)
                .ThenInclude(a => a.AccountRoles)
                    .ThenInclude(ar => ar.Role)
            .ToListAsync();

        return Ok(new PagedResponse<UserResponse>
        {
            Items = users.Select(MapUser).ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }

    /// <summary>
    /// Get user by ID. System only.
    /// </summary>
    [HttpGet("{id}")]
    [RequirePermission(Permissions.Users.Read)]
    public async Task<ActionResult<UserResponse>> GetUser(string id)
    {
        var user = await _db.Users
            .Include(u => u.Accounts)
                .ThenInclude(a => a.AccountRoles)
                    .ThenInclude(ar => ar.Role)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
            return NotFoundError("User not found.");

        return Ok(MapUser(user));
    }

    /// <summary>
    /// Create a new user with Customer account. System only.
    /// </summary>
    [HttpPost]
    [RequirePermission(Permissions.Users.Create)]
    public async Task<ActionResult<UserResponse>> CreateUser([FromBody] CreateUserRequest request)
    {
        // Check if user exists
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing != null)
            return Conflict("A user with this email already exists.");

        // Create user
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            CreatedAt = DateTime.UtcNow
        };

        var result = string.IsNullOrEmpty(request.Password)
            ? await _userManager.CreateAsync(user)
            : await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            return BadRequest(new ApiError
            {
                Error = "Failed to create user",
                Errors = result.Errors.GroupBy(e => e.Code)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray())
            });
        }

        // Get default Customer role
        var customerRole = await _db.AppRoles
            .FirstOrDefaultAsync(r => r.TenantType == TenantType.Customer && r.IsSystem && r.Name == "Customer");

        // Auto-create Customer account
        var customerAccount = new Account
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantType = TenantType.Customer,
            TenantId = null,
            DisplayName = user.FullName,
            Status = AccountStatus.Active,
            CreatedAt = DateTime.UtcNow,
            CreatedByAccountId = CurrentAccountId
        };

        _db.Accounts.Add(customerAccount);

        // Assign default customer role if exists
        if (customerRole != null)
        {
            _db.AccountRoles.Add(new AccountRole
            {
                Id = Guid.NewGuid(),
                AccountId = customerAccount.Id,
                RoleId = customerRole.Id,
                AssignedAt = DateTime.UtcNow,
                AssignedByAccountId = CurrentAccountId
            });
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("User {UserId} created with Customer account by {AccountId}",
            user.Id, CurrentAccountId);

        // Reload with accounts
        var created = await _db.Users
            .Include(u => u.Accounts)
                .ThenInclude(a => a.AccountRoles)
                    .ThenInclude(ar => ar.Role)
            .FirstAsync(u => u.Id == user.Id);

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, MapUser(created));
    }

    /// <summary>
    /// Update user details. System only.
    /// </summary>
    [HttpPut("{id}")]
    [RequirePermission(Permissions.Users.Update)]
    public async Task<ActionResult<UserResponse>> UpdateUser(string id, [FromBody] UpdateUserRequest request)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFoundError("User not found.");

        if (request.FirstName != null) user.FirstName = request.FirstName;
        if (request.LastName != null) user.LastName = request.LastName;

        await _userManager.UpdateAsync(user);

        _logger.LogInformation("User {UserId} updated by {AccountId}", id, CurrentAccountId);

        var updated = await _db.Users
            .Include(u => u.Accounts)
                .ThenInclude(a => a.AccountRoles)
                    .ThenInclude(ar => ar.Role)
            .FirstAsync(u => u.Id == id);

        return Ok(MapUser(updated));
    }

    /// <summary>
    /// Lock or unlock a user. System only.
    /// </summary>
    [HttpPost("{id}/lock")]
    [RequirePermission(Permissions.Users.Lock)]
    public async Task<ActionResult<UserResponse>> LockUser(string id, [FromBody] LockUserRequest request)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFoundError("User not found.");

        if (request.Lock)
        {
            var lockoutEnd = request.Duration.HasValue
                ? DateTimeOffset.UtcNow.Add(request.Duration.Value)
                : DateTimeOffset.MaxValue;

            await _userManager.SetLockoutEndDateAsync(user, lockoutEnd);

            // Update user's security stamp to invalidate tokens
            await _userManager.UpdateSecurityStampAsync(user);

            _logger.LogInformation("User {UserId} locked by {AccountId}. Reason: {Reason}",
                id, CurrentAccountId, request.Reason);
        }
        else
        {
            await _userManager.SetLockoutEndDateAsync(user, null);
            _logger.LogInformation("User {UserId} unlocked by {AccountId}", id, CurrentAccountId);
        }

        var updated = await _db.Users
            .Include(u => u.Accounts)
                .ThenInclude(a => a.AccountRoles)
                    .ThenInclude(ar => ar.Role)
            .FirstAsync(u => u.Id == id);

        return Ok(MapUser(updated));
    }

    /// <summary>
    /// Delete a user (soft delete - locks and marks accounts as deleted). System only.
    /// </summary>
    [HttpDelete("{id}")]
    [RequirePermission(Permissions.Users.Delete)]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFoundError("User not found.");

        // Lock user permanently
        await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

        // Update user's security stamp to invalidate all tokens
        await _userManager.UpdateSecurityStampAsync(user);

        // Mark all accounts as deleted and refresh their security stamps
        var accounts = await _db.Accounts.Where(a => a.UserId == id).ToListAsync();
        foreach (var account in accounts)
        {
            account.Status = AccountStatus.Deleted;
            account.RefreshSecurityStamp();
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("User {UserId} deleted by {AccountId}", id, CurrentAccountId);

        return NoContent();
    }

    private static UserResponse MapUser(ApplicationUser user)
    {
        return new UserResponse
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = user.FullName,
            EmailConfirmed = user.EmailConfirmed,
            IsLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow,
            LockoutEnd = user.LockoutEnd,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
            Accounts = user.Accounts.Select(a => new AccountSummary
            {
                Id = a.Id,
                TenantType = a.TenantType,
                TenantId = a.TenantId,
                DisplayName = a.DisplayName,
                Status = a.Status,
                Roles = a.AccountRoles.Select(ar => ar.Role?.Name ?? "").Where(n => !string.IsNullOrEmpty(n)).ToList()
            }).ToList()
        };
    }
}
