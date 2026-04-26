using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using IdentityServer.Data.Entities;
using IdentityServer.Models;
using Microsoft.AspNetCore.Identity;

namespace IdentityServer.Services;

/// <summary>
/// Custom profile service that injects account, role, and permission claims into tokens.
/// Also validates user and account security status for token refresh.
/// </summary>
public class CustomProfileService : IProfileService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAccountService _accountService;
    private readonly ILogger<CustomProfileService> _logger;

    public CustomProfileService(
        UserManager<ApplicationUser> userManager,
        IAccountService accountService,
        ILogger<CustomProfileService> logger)
    {
        _userManager = userManager;
        _accountService = accountService;
        _logger = logger;
    }

    /// <summary>
    /// Called when IdentityServer needs to determine which claims to include in tokens.
    /// </summary>
    public async Task GetProfileDataAsync(ProfileDataRequestContext context)
    {
        var userId = context.Subject.GetSubjectId();

        _logger.LogDebug("Getting profile data for user {UserId}, caller: {Caller}", 
            userId, context.Caller);

        // Get user to include security stamp
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found when getting profile data", userId);
            return;
        }

        // Add user security stamp hash to token (for validation on refresh)
        var userStampHash = HashSecurityStamp(user.SecurityStamp ?? string.Empty);
        context.IssuedClaims.Add(new Claim("user_stamp", userStampHash));

        // Try to get account_id from:
        // 1. Existing claims (set during login or token exchange)
        // 2. Auto-select first account if user has accounts
        Guid? accountId = null;

        // Check existing claims first
        var accountIdClaim = context.Subject.FindFirst("account_id")?.Value;
        if (!string.IsNullOrEmpty(accountIdClaim) && Guid.TryParse(accountIdClaim, out var parsedId))
        {
            accountId = parsedId;
            _logger.LogDebug("Found existing account_id claim: {AccountId}", accountId);
        }

        // If no account selected, auto-select the first active account
        if (accountId == null)
        {
            var accounts = await _accountService.GetUserAccountsAsync(userId);
            var firstActiveAccount = accounts.FirstOrDefault(a => a.Status == AccountStatus.Active);

            if (firstActiveAccount != null)
            {
                accountId = firstActiveAccount.Id;
                _logger.LogDebug("Auto-selected account {AccountId} for user {UserId}", 
                    accountId, userId);
            }
            else
            {
                _logger.LogWarning("No active accounts found for user {UserId}", userId);
            }
        }

        // Add account claims if we have an account
        if (accountId.HasValue)
        {
            await AddAccountClaimsAsync(context, accountId.Value);
        }

        // Always add available accounts (for UI to show account switcher)
        await AddAvailableAccountsClaimAsync(context, userId);
    }

    /// <summary>
    /// Called to validate if the user/account is still active.
    /// This is called on EVERY refresh token request.
    /// </summary>
    public async Task IsActiveAsync(IsActiveContext context)
    {
        var userId = context.Subject.GetSubjectId();

        _logger.LogDebug("Checking if user {UserId} is active for {Caller}", userId, context.Caller);

        // === Step 1: Validate User ===
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found - marking as inactive", userId);
            context.IsActive = false;
            return;
        }

        // Check if user is locked out
        if (await _userManager.IsLockedOutAsync(user))
        {
            _logger.LogWarning("User {UserId} is locked out - marking as inactive", userId);
            context.IsActive = false;
            return;
        }

        // Check user security stamp (if present in token)
        var tokenUserStamp = context.Subject.FindFirst("user_stamp")?.Value;
        if (!string.IsNullOrEmpty(tokenUserStamp))
        {
            var currentStampHash = HashSecurityStamp(user.SecurityStamp ?? string.Empty);
            if (tokenUserStamp != currentStampHash)
            {
                _logger.LogWarning("User {UserId} security stamp mismatch - credentials changed", userId);
                context.IsActive = false;
                return;
            }
        }

        // === Step 2: Validate Account ===
        var accountIdClaim = context.Subject.FindFirst("account_id")?.Value;
        if (!string.IsNullOrEmpty(accountIdClaim) && Guid.TryParse(accountIdClaim, out var accountId))
        {
            var account = await _accountService.GetAccountByIdAsync(accountId);

            if (account == null)
            {
                _logger.LogWarning("Account {AccountId} not found - marking as inactive", accountId);
                context.IsActive = false;
                return;
            }

            // Check account status
            if (account.Status != AccountStatus.Active)
            {
                _logger.LogWarning("Account {AccountId} status is {Status} - marking as inactive", 
                    accountId, account.Status);
                context.IsActive = false;
                return;
            }

            // Check account security stamp (if present in token)
            var tokenAccountStamp = context.Subject.FindFirst("account_stamp")?.Value;
            if (!string.IsNullOrEmpty(tokenAccountStamp))
            {
                var currentAccountStampHash = HashSecurityStamp(account.SecurityStamp);
                if (tokenAccountStamp != currentAccountStampHash)
                {
                    _logger.LogWarning("Account {AccountId} security stamp mismatch - roles/permissions changed", accountId);
                    context.IsActive = false;
                    return;
                }
            }

            _logger.LogDebug("Account {AccountId} is active", accountId);
        }
        else
        {
            // No account selected - check if user has any active accounts
            var accounts = await _accountService.GetUserAccountsAsync(userId);
            if (!accounts.Any(a => a.Status == AccountStatus.Active))
            {
                _logger.LogWarning("User {UserId} has no active accounts", userId);
                context.IsActive = false;
                return;
            }
        }

        context.IsActive = true;
        _logger.LogDebug("User {UserId} is active", userId);
    }

    private async Task AddAccountClaimsAsync(ProfileDataRequestContext context, Guid accountId)
    {
        var account = await _accountService.GetAccountWithDetailsAsync(accountId);

        if (account == null)
        {
            _logger.LogWarning("Account {AccountId} not found when adding claims", accountId);
            return;
        }

        _logger.LogDebug("Adding claims for account {AccountId}, tenant type {TenantType}", 
            accountId, account.TenantType);

        // Account context claims
        context.IssuedClaims.Add(new Claim("account_id", account.Id.ToString()));
        context.IssuedClaims.Add(new Claim("tenant_id", account.TenantId?.ToString() ?? string.Empty));
        context.IssuedClaims.Add(new Claim("tenant_type", account.TenantType.ToString().ToLowerInvariant()));

        // Account security stamp hash (for validation on refresh)
        var accountStampHash = HashSecurityStamp(account.SecurityStamp);
        context.IssuedClaims.Add(new Claim("account_stamp", accountStampHash));

        if (!string.IsNullOrEmpty(account.DisplayName))
        {
            context.IssuedClaims.Add(new Claim("account_name", account.DisplayName));
        }

        // Role claims
        var roles = account.AccountRoles
            .Where(ar => ar.Role != null)
            .Select(ar => ar.Role!)
            .ToList();

        _logger.LogDebug("Account {AccountId} has {RoleCount} roles", accountId, roles.Count);

        foreach (var role in roles)
        {
            context.IssuedClaims.Add(new Claim("role", role.Name));
            _logger.LogDebug("Added role claim: {RoleName}", role.Name);
        }

        // Permission claims (flattened from roles)
        var permissions = roles
            .SelectMany(r => r.GetPermissions())
            .Distinct()
            .ToList();

        _logger.LogDebug("Account {AccountId} has {PermissionCount} permissions", accountId, permissions.Count);

        foreach (var permission in permissions)
        {
            context.IssuedClaims.Add(new Claim("permission", permission));
        }

        _logger.LogInformation("Added {RoleCount} roles and {PermissionCount} permissions for account {AccountId}",
            roles.Count, permissions.Count, accountId);
    }

    private async Task AddAvailableAccountsClaimAsync(ProfileDataRequestContext context, string userId)
    {
        var accounts = await _accountService.GetUserAccountsAsync(userId);

        var availableAccounts = accounts
            .Where(a => a.Status == AccountStatus.Active)
            .Select(a => new
            {
                id = a.Id.ToString(),
                tenantId = a.TenantId?.ToString() ?? string.Empty,
                tenantType = a.TenantType.ToString().ToLowerInvariant(),
                displayName = a.DisplayName ?? a.TenantType.ToString()
            })
            .ToList();

        if (availableAccounts.Count > 0)
        {
            var json = JsonSerializer.Serialize(availableAccounts);
            context.IssuedClaims.Add(new Claim("available_accounts", json, "json"));
            _logger.LogDebug("Added {Count} available accounts to claims", availableAccounts.Count);
        }
    }

    /// <summary>
    /// Creates a short hash of the security stamp for token inclusion.
    /// We don't include the full stamp to avoid leaking it.
    /// </summary>
    private static string HashSecurityStamp(string securityStamp)
    {
        if (string.IsNullOrEmpty(securityStamp))
            return string.Empty;

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(securityStamp));
        // Take first 16 bytes and convert to base64 (shorter token)
        return Convert.ToBase64String(bytes, 0, 16);
    }
}
