using IdentityServer.Api.Models;
using IdentityServer.Data;
using IdentityServer.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Api.Controllers;

/// <summary>
/// User management endpoints.
/// </summary>
[Route("api/users")]
public class UsersController : ApiControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        ILogger<UsersController> logger)
    {
        _userManager = userManager;
        _db = db;
        _logger = logger;
    }

    [HttpGet]
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
            .ToListAsync();

        return Ok(new PagedResponse<UserResponse>
        {
            Items = users.Select(MapUser).ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserResponse>> GetUser(string id)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
            return NotFoundError("User not found.");

        return Ok(MapUser(user));
    }

    [HttpPost]
    public async Task<ActionResult<UserResponse>> CreateUser([FromBody] CreateUserRequest request)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing != null)
            return Conflict("A user with this email already exists.");

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

        _logger.LogInformation("User {UserId} created by {CurrentUserId}", user.Id, CurrentUserId);

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, MapUser(user));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<UserResponse>> UpdateUser(string id, [FromBody] UpdateUserRequest request)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFoundError("User not found.");

        if (request.FirstName != null) user.FirstName = request.FirstName;
        if (request.LastName != null) user.LastName = request.LastName;

        await _userManager.UpdateAsync(user);

        _logger.LogInformation("User {UserId} updated by {CurrentUserId}", id, CurrentUserId);

        return Ok(MapUser(user));
    }

    [HttpPost("{id}/lock")]
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
            await _userManager.UpdateSecurityStampAsync(user);

            _logger.LogInformation("User {UserId} locked by {CurrentUserId}. Reason: {Reason}",
                id, CurrentUserId, request.Reason);
        }
        else
        {
            await _userManager.SetLockoutEndDateAsync(user, null);
            _logger.LogInformation("User {UserId} unlocked by {CurrentUserId}", id, CurrentUserId);
        }

        var updated = await _userManager.FindByIdAsync(id);
        return Ok(MapUser(updated!));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFoundError("User not found.");

        await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        await _userManager.UpdateSecurityStampAsync(user);

        _logger.LogInformation("User {UserId} deleted by {CurrentUserId}", id, CurrentUserId);

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
            LastLoginAt = user.LastLoginAt
        };
    }
}
