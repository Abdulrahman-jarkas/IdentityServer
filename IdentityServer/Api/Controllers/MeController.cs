using IdentityServer.Api.Models;
using IdentityServer.Authorization;
using IdentityServer.Data;
using IdentityServer.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace IdentityServer.Api.Controllers;

/// <summary>
/// Current user endpoints. Any authenticated user can access their own data.
/// Data is scoped to the current tenant context.
/// </summary>
[Route("api/me")]
public class MeController : ApiControllerBase
{
    private readonly ApplicationDbContext _db;

    public MeController(
        ITenantContext tenantContext,
        ApplicationDbContext db) : base(tenantContext)
    {
        _db = db;
    }

    /// <summary>
    /// Get current user info with current account only.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<MeResponse>> GetMe()
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == CurrentUserId);

        if (user == null)
            return NotFoundError("User not found.");

        // Get current account based on tenant context
        AccountResponse? currentAccount = null;
        if (CurrentAccountId.HasValue)
        {
            var account = await _db.Accounts
                .Include(a => a.AccountRoles)
                    .ThenInclude(ar => ar.Role)
                .FirstOrDefaultAsync(a => a.Id == CurrentAccountId && a.UserId == CurrentUserId);

            if (account != null)
            {
                currentAccount = MapAccount(account);
            }
        }

        // Only return accounts matching current tenant type
        var scopedAccounts = await GetScopedAccountsAsync();

        return Ok(new MeResponse
        {
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            CurrentAccount = currentAccount,
            AvailableAccounts = scopedAccounts.Select(a => new AccountSummary
            {
                Id = a.Id,
                TenantType = a.TenantType,
                TenantId = a.TenantId,
                DisplayName = a.DisplayName,
                Status = a.Status,
                Roles = a.AccountRoles.Select(ar => ar.Role?.Name ?? "").Where(n => !string.IsNullOrEmpty(n)).ToList()
            }).ToList()
        });
    }

    /// <summary>
    /// Get current user's accounts scoped to current tenant type.
    /// </summary>
    [HttpGet("accounts")]
    public async Task<ActionResult<List<AccountSummary>>> GetMyAccounts()
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var accounts = await GetScopedAccountsAsync();

        return Ok(accounts.Select(a => new AccountSummary
        {
            Id = a.Id,
            TenantType = a.TenantType,
            TenantId = a.TenantId,
            DisplayName = a.DisplayName,
            Status = a.Status,
            Roles = a.AccountRoles.Select(ar => ar.Role?.Name ?? "").Where(n => !string.IsNullOrEmpty(n)).ToList()
        }).ToList());
    }

    /// <summary>
    /// Get specific account details (only if belongs to current user and matches tenant scope).
    /// </summary>
    [HttpGet("accounts/{id:guid}")]
    public async Task<ActionResult<AccountResponse>> GetMyAccount(Guid id)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var account = await _db.Accounts
            .Include(a => a.User)
            .Include(a => a.AccountRoles)
                .ThenInclude(ar => ar.Role)
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == CurrentUserId);

        if (account == null)
            return NotFoundError("Account not found.");

        // Verify account matches current tenant scope
        if (!IsAccountInScope(account))
            return NotFoundError("Account not found.");

        return Ok(MapAccount(account));
    }

    /// <summary>
    /// Get current account's permissions.
    /// </summary>
    [HttpGet("permissions")]
    public ActionResult<List<string>> GetMyPermissions()
    {
        return Ok(TenantContext.Permissions.ToList());
    }

    /// <summary>
    /// Get accounts scoped to the current tenant context.
    /// </summary>
    private async Task<List<Account>> GetScopedAccountsAsync()
    {
        var query = _db.Accounts
            .Include(a => a.AccountRoles)
                .ThenInclude(ar => ar.Role)
            .Where(a => a.UserId == CurrentUserId && a.Status == AccountStatus.Active);

        // Scope based on current tenant type
        if (CurrentTenantType.HasValue)
        {
            query = query.Where(a => a.TenantType == CurrentTenantType.Value);

            // Further scope by tenant ID for non-global tenant types
            if (CurrentTenantId.HasValue)
            {
                query = query.Where(a => a.TenantId == CurrentTenantId.Value);
            }
        }

        return await query.ToListAsync();
    }

    /// <summary>
    /// Check if an account is within the current tenant scope.
    /// </summary>
    private bool IsAccountInScope(Account account)
    {
        if (!CurrentTenantType.HasValue)
            return true; // No tenant context, allow all

        if (account.TenantType != CurrentTenantType.Value)
            return false;

        // For tenant types with specific IDs (Shop, Warehouse), check tenant ID
        if (CurrentTenantId.HasValue && account.TenantId != CurrentTenantId.Value)
            return false;

        return true;
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

    /// <summary>
    /// Switch to a different account. Returns new access token for the target account.
    /// Uses RFC 8693 Token Exchange flow internally.
    /// </summary>
    /// <param name="request">The account switch request containing the target account ID</param>
    /// <returns>New access token for the target account</returns>
    [HttpPost("switch-account")]
    [ProducesResponseType(typeof(SwitchAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SwitchAccountResponse>> SwitchAccount([FromBody] SwitchAccountRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        // Validate the target account exists and belongs to current user
        var targetAccount = await _db.Accounts
            .Include(a => a.AccountRoles)
                .ThenInclude(ar => ar.Role)
            .FirstOrDefaultAsync(a => a.Id == request.AccountId && a.UserId == CurrentUserId);

        if (targetAccount == null)
            return NotFoundError("Account not found or does not belong to you.");

        if (targetAccount.Status != AccountStatus.Active)
            return BadRequestError("Account is not active.");

        // Get the current access token from the Authorization header
        var authHeader = HttpContext.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return BadRequestError("Bearer token is required.");

        var currentToken = authHeader["Bearer ".Length..].Trim();

        // Call the token endpoint with token exchange grant
        using var httpClient = new HttpClient();
        var tokenEndpoint = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/connect/token";

        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:token-exchange",
            ["subject_token"] = currentToken,
            ["subject_token_type"] = "urn:ietf:params:oauth:token-type:access_token",
            ["account_id"] = request.AccountId.ToString(),
            ["client_id"] = "swagger.ui",
            ["scope"] = "openid profile email account talabat.api"
        });

        var response = await httpClient.PostAsync(tokenEndpoint, tokenRequest);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var errorResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
            var errorDescription = errorResponse.TryGetProperty("error_description", out var desc) 
                ? desc.GetString() 
                : "Token exchange failed";
            return BadRequestError(errorDescription ?? "Token exchange failed");
        }

        var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

        return Ok(new SwitchAccountResponse
        {
            AccessToken = tokenResponse.GetProperty("access_token").GetString()!,
            RefreshToken = tokenResponse.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null,
            ExpiresIn = tokenResponse.GetProperty("expires_in").GetInt32(),
            TokenType = tokenResponse.GetProperty("token_type").GetString()!,
            Account = new AccountSummary
            {
                Id = targetAccount.Id,
                TenantType = targetAccount.TenantType,
                TenantId = targetAccount.TenantId,
                DisplayName = targetAccount.DisplayName,
                Status = targetAccount.Status,
                Roles = targetAccount.AccountRoles
                    .Where(ar => ar.Role != null)
                    .Select(ar => ar.Role!.Name)
                    .ToList()
            }
        });
    }
}
