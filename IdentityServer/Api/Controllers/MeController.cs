using IdentityServer.Api.Models;
using IdentityServer.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Api.Controllers;

/// <summary>
/// Current user endpoints. Any authenticated user can access their own data.
/// </summary>
[Route("api/me")]
public class MeController : ApiControllerBase
{
    private readonly ApplicationDbContext _db;

    public MeController(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Get current user info.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<MeResponse>> GetMe()
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == CurrentUserId);

        if (user == null)
            return NotFoundError("User not found.");

        return Ok(new MeResponse
        {
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName
        });
    }

    /// <summary>
    /// Get current user's permissions from token claims.
    /// </summary>
    [HttpGet("permissions")]
    public ActionResult<List<string>> GetMyPermissions()
    {
        var permissions = User.FindAll("permission").Select(c => c.Value).ToList();
        return Ok(permissions);
    }
}