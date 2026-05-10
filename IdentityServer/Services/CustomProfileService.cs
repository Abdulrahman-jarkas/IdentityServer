using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using IdentityServer.Models;
using Microsoft.AspNetCore.Identity;

namespace IdentityServer.Services;

/// <summary>
/// Two-phase token flow:
///   Login   -> token with sub + user_stamp only (no account)
///   Exchange -> BFF passes account_id + account_version -> added to claims
///   Refresh  -> preserves account claims from existing token
/// </summary>
public class CustomProfileService : IProfileService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<CustomProfileService> _logger;

    public CustomProfileService(
        UserManager<ApplicationUser> userManager,
        ILogger<CustomProfileService> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    public async Task GetProfileDataAsync(ProfileDataRequestContext context)
    {
        var userId = context.Subject.GetSubjectId();

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found", userId);
            return;
        }

        // Always add security stamp for token invalidation
        var userStampHash = HashSecurityStamp(user.SecurityStamp ?? string.Empty);
        context.IssuedClaims.Add(new Claim("user_stamp", userStampHash));

        // Preserve account claims if present (from token exchange or refresh)
        var existingAccountId = context.Subject.FindFirst("account_id")?.Value;
        var existingAccountVersion = context.Subject.FindFirst("account_version")?.Value;

        if (!string.IsNullOrEmpty(existingAccountId))
        {
            context.IssuedClaims.Add(new Claim("account_id", existingAccountId));
            context.IssuedClaims.Add(new Claim("account_version", existingAccountVersion ?? "0"));
        }

        // First login: no account claims - BFF calls GET /api/accounts on Talabat API,
        // then does token exchange with account_id + account_version
    }

    public async Task IsActiveAsync(IsActiveContext context)
    {
        var userId = context.Subject.GetSubjectId();

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            context.IsActive = false;
            return;
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            context.IsActive = false;
            return;
        }

        // Validate user security stamp
        var tokenUserStamp = context.Subject.FindFirst("user_stamp")?.Value;
        if (!string.IsNullOrEmpty(tokenUserStamp))
        {
            var currentStampHash = HashSecurityStamp(user.SecurityStamp ?? string.Empty);
            if (tokenUserStamp != currentStampHash)
            {
                _logger.LogWarning("User {UserId} security stamp mismatch", userId);
                context.IsActive = false;
                return;
            }
        }

        context.IsActive = true;
    }

    private static string HashSecurityStamp(string securityStamp)
    {
        if (string.IsNullOrEmpty(securityStamp))
            return string.Empty;

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(securityStamp));
        return Convert.ToBase64String(bytes, 0, 16);
    }
}
