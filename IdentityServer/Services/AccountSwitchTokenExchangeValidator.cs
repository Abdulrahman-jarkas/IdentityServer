using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using IdentityServer.Data;
using IdentityServer.Data.Entities;
using IdentityServer.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace IdentityServer.Services;

/// <summary>
/// Extension grant validator for RFC 8693 Token Exchange with account switching.
/// 
/// Token validation is delegated to Duende's ITokenValidator which handles:
/// - Signature validation
/// - Token expiration
/// - Issuer validation
/// - Audience validation
/// 
/// We add:
/// - User lockout validation
/// - Account status validation
/// - Security stamp validation
/// - Account claims building
/// </summary>
public class AccountSwitchTokenExchangeValidator : IExtensionGrantValidator
{
    private readonly ITokenValidator _tokenValidator;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<AccountSwitchTokenExchangeValidator> _logger;

    public string GrantType => "urn:ietf:params:oauth:grant-type:token-exchange";

    public AccountSwitchTokenExchangeValidator(
        ITokenValidator tokenValidator,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        ILogger<AccountSwitchTokenExchangeValidator> logger)
    {
        _tokenValidator = tokenValidator;
        _userManager = userManager;
        _db = db;
        _logger = logger;
    }

    public async Task ValidateAsync(ExtensionGrantValidationContext context)
    {
        // === Step 1: Validate RFC 8693 required parameters ===
        var subjectToken = context.Request.Raw.Get("subject_token");
        var subjectTokenType = context.Request.Raw.Get("subject_token_type");

        if (string.IsNullOrEmpty(subjectToken))
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidRequest, "subject_token is required");
            return;
        }

        if (subjectTokenType != "urn:ietf:params:oauth:token-type:access_token")
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidRequest, 
                "subject_token_type must be urn:ietf:params:oauth:token-type:access_token");
            return;
        }

        // === Step 2: Delegate token validation to Duende's ITokenValidator ===
        // This handles: signature, expiration, issuer, audience, token type validation
        var tokenValidationResult = await _tokenValidator.ValidateAccessTokenAsync(subjectToken);

        if (tokenValidationResult.IsError)
        {
            _logger.LogWarning("Token exchange failed: {Error}", tokenValidationResult.Error);
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "invalid subject_token");
            return;
        }

        // === Step 3: Extract user identity from validated token ===
        var userId = tokenValidationResult.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "subject token missing sub claim");
            return;
        }

        // === Step 4: Validate User is not locked ===
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("Token exchange failed: user {UserId} not found", userId);
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "user not found");
            return;
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            _logger.LogWarning("Token exchange failed: user {UserId} is locked out", userId);
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "user is locked");
            return;
        }

        // === Step 5: Validate account_id (required parameter) ===
        var targetAccountId = context.Request.Raw.Get("account_id");

        if (string.IsNullOrEmpty(targetAccountId))
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidRequest, "account_id is required");
            return;
        }

        if (!Guid.TryParse(targetAccountId, out var accountId))
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidRequest, "account_id must be a valid GUID");
            return;
        }

        var account = await _db.Accounts
            .Include(a => a.AccountRoles)
                .ThenInclude(ar => ar.Role)
            .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId);

        if (account == null)
        {
            _logger.LogWarning("Token exchange failed: account {AccountId} not found for user {UserId}", accountId, userId);
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "account not found");
            return;
        }

        // === Step 6: Verify account is active ===
        if (account.Status != AccountStatus.Active)
        {
            _logger.LogWarning("Token exchange failed: account {AccountId} is not active", account.Id);
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "account is not active");
            return;
        }

        _logger.LogInformation("Token exchange: user {UserId} using account {AccountId} ({TenantType})", 
            userId, account.Id, account.TenantType);

        // === Step 7: Build claims and return success ===
        var claims = BuildAccountClaims(account, user);

        context.Result = new GrantValidationResult(
            subject: userId,
            authenticationMethod: GrantType,
            claims: claims
        );
    }

    private static List<Claim> BuildAccountClaims(Account account, ApplicationUser user)
    {
        var claims = new List<Claim>
        {
            new Claim("account_id", account.Id.ToString()),
            new Claim("tenant_id", account.TenantId?.ToString() ?? string.Empty),
            new Claim("tenant_type", account.TenantType.ToString().ToLowerInvariant()),
            // Security stamps for validation on refresh
            new Claim("user_stamp", HashSecurityStamp(user.SecurityStamp ?? string.Empty)),
            new Claim("account_stamp", HashSecurityStamp(account.SecurityStamp))
        };

        if (!string.IsNullOrEmpty(account.DisplayName))
        {
            claims.Add(new Claim("account_name", account.DisplayName));
        }

        // Add role claims
        var roles = account.AccountRoles
            .Where(ar => ar.Role != null)
            .Select(ar => ar.Role!)
            .ToList();

        foreach (var role in roles)
        {
            claims.Add(new Claim("role", role.Name));
        }

        // Add permission claims
        var permissions = roles
            .SelectMany(r => r.GetPermissions())
            .Distinct();

        foreach (var permission in permissions)
        {
            claims.Add(new Claim("permission", permission));
        }

        return claims;
    }

    /// <summary>
    /// Creates a short hash of the security stamp for token inclusion.
    /// </summary>
    private static string HashSecurityStamp(string securityStamp)
    {
        if (string.IsNullOrEmpty(securityStamp))
            return string.Empty;

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(securityStamp));
        return Convert.ToBase64String(bytes, 0, 16);
    }
}
