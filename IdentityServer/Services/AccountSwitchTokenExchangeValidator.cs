using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using IdentityServer.Models;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace IdentityServer.Services;

/// <summary>
/// Extension grant validator for RFC 8693 Token Exchange with account switching.
/// BFF server passes account_id + account_version (obtained from Talabat API).
/// IS trusts the BFF (confidential client with client_secret) and stamps the claims.
/// Talabat API enforces account ownership on every request.
/// </summary>
public class AccountSwitchTokenExchangeValidator : IExtensionGrantValidator
{
    private readonly ITokenValidator _tokenValidator;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AccountSwitchTokenExchangeValidator> _logger;

    public string GrantType => "urn:ietf:params:oauth:grant-type:token-exchange";

    public AccountSwitchTokenExchangeValidator(
        ITokenValidator tokenValidator,
        UserManager<ApplicationUser> userManager,
        ILogger<AccountSwitchTokenExchangeValidator> logger)
    {
        _tokenValidator = tokenValidator;
        _userManager = userManager;
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

        // === Step 2: Validate the subject token ===
        var tokenValidationResult = await _tokenValidator.ValidateAccessTokenAsync(subjectToken);

        if (tokenValidationResult.IsError)
        {
            _logger.LogWarning("Token exchange failed: {Error}", tokenValidationResult.Error);
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "invalid subject_token");
            return;
        }

        // === Step 3: Extract user identity ===
        var userId = tokenValidationResult.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "subject token missing sub claim");
            return;
        }

        // === Step 4: Validate user is not locked ===
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "user not found");
            return;
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "user is locked");
            return;
        }

        // Validate user security stamp
        var tokenUserStamp = tokenValidationResult.Claims.FirstOrDefault(c => c.Type == "user_stamp")?.Value;
        if (!string.IsNullOrEmpty(tokenUserStamp))
        {
            var currentStampHash = HashSecurityStamp(user.SecurityStamp ?? string.Empty);
            if (tokenUserStamp != currentStampHash)
            {
                _logger.LogWarning("User {UserId} security stamp mismatch", userId);
                context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "security stamp mismatch");
                return;
            }
        }

        // === Step 5: Validate account_id parameter ===
        var targetAccountId = context.Request.Raw.Get("account_id");

        if (string.IsNullOrEmpty(targetAccountId) || !Guid.TryParse(targetAccountId, out var accountId))
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidRequest, "account_id is required and must be a valid GUID");
            return;
        }

        // === Step 6: Validate account_version parameter ===
        var targetAccountVersion = context.Request.Raw.Get("account_version");

        if (string.IsNullOrEmpty(targetAccountVersion))
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidRequest, "account_version is required");
            return;
        }

        _logger.LogInformation("Token exchange: user {UserId} switching to account {AccountId} v{Version}",
            userId, accountId, targetAccountVersion);

        // === Step 7: Build claims and return success ===
        var claims = new List<Claim>
        {
            new("account_id", accountId.ToString()),
            new("account_version", targetAccountVersion)
        };

        context.Result = new GrantValidationResult(
            subject: userId,
            authenticationMethod: GrantType,
            claims: claims
        );
    }

    private static string HashSecurityStamp(string securityStamp)
    {
        if (string.IsNullOrEmpty(securityStamp))
            return string.Empty;

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(securityStamp));
        return Convert.ToBase64String(bytes, 0, 16);
    }
}
