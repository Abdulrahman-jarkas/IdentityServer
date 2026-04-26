using System.ComponentModel.DataAnnotations;
using IdentityServer.Data.Entities;

namespace IdentityServer.Api.Models;

// ============================================
// COMMON
// ============================================

public class PagedRequest
{
    private int _page = 1;
    private int _pageSize = 20;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? 1 : (value > 100 ? 100 : value);
    }

    public string? Search { get; set; }
}

public class PagedResponse<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

public class ApiError
{
    public required string Error { get; set; }
    public string? Detail { get; set; }
    public Dictionary<string, string[]>? Errors { get; set; }
}

// ============================================
// USER
// ============================================

public class CreateUserRequest
{
    [Required, EmailAddress]
    public required string Email { get; set; }

    [Required, MaxLength(100)]
    public required string FirstName { get; set; }

    [Required, MaxLength(100)]
    public required string LastName { get; set; }

    /// <summary>
    /// Optional password. If not provided, user must reset password.
    /// </summary>
    [MinLength(8)]
    public string? Password { get; set; }
}

public class UpdateUserRequest
{
    [MaxLength(100)]
    public string? FirstName { get; set; }

    [MaxLength(100)]
    public string? LastName { get; set; }
}

public class LockUserRequest
{
    public bool Lock { get; set; }
    public TimeSpan? Duration { get; set; }
    public string? Reason { get; set; }
}

public class UserResponse
{
    public required string Id { get; set; }
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? FullName { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool IsLockedOut { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public List<AccountSummary> Accounts { get; set; } = new();
}

// ============================================
// ACCOUNT
// ============================================

public class CreateAccountRequest
{
    [Required]
    public required string UserId { get; set; }

    /// <summary>
    /// Required for Shop, Driver (with fleet), Warehouse accounts.
    /// </summary>
    public Guid? TenantId { get; set; }

    [MaxLength(200)]
    public string? DisplayName { get; set; }

    public List<Guid> RoleIds { get; set; } = new();
}

public class UpdateAccountRequest
{
    [MaxLength(200)]
    public string? DisplayName { get; set; }

    public List<Guid>? RoleIds { get; set; }
}

public class AccountResponse
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? UserFullName { get; set; }
    public TenantType TenantType { get; set; }
    public Guid? TenantId { get; set; }
    public string? DisplayName { get; set; }
    public AccountStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<RoleSummary> Roles { get; set; } = new();
    public List<string> Permissions { get; set; } = new();
}

public class AccountSummary
{
    public Guid Id { get; set; }
    public TenantType TenantType { get; set; }
    public Guid? TenantId { get; set; }
    public string? DisplayName { get; set; }
    public AccountStatus Status { get; set; }
    public List<string> Roles { get; set; } = new();
}

// ============================================
// ROLE
// ============================================

public class CreateRoleRequest
{
    [Required, MaxLength(100)]
    public required string Name { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Specific tenant's custom role. Null for template roles.
    /// </summary>
    public Guid? TenantId { get; set; }

    public List<string> Permissions { get; set; } = new();
}

public class UpdateRoleRequest
{
    [Required, MaxLength(100)]
    public required string Name { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public List<string> Permissions { get; set; } = new();
}

public class RoleResponse
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public TenantType TenantType { get; set; }
    public Guid? TenantId { get; set; }
    public bool IsSystem { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> Permissions { get; set; } = new();
    public int AccountCount { get; set; }
}

public class RoleSummary
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
}

// ============================================
// ME (Self-access)
// ============================================

public class MeResponse
{
    public required string UserId { get; set; }
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public AccountResponse? CurrentAccount { get; set; }
    public List<AccountSummary> AvailableAccounts { get; set; } = new();
}

// ============================================
// SWITCH ACCOUNT
// ============================================

/// <summary>
/// Request to switch to a different account.
/// </summary>
public class SwitchAccountRequest
{
    /// <summary>
    /// The ID of the account to switch to. Must belong to the current user.
    /// </summary>
    [Required]
    public Guid AccountId { get; set; }
}

/// <summary>
/// Response containing new tokens after switching accounts.
/// </summary>
public class SwitchAccountResponse
{
    /// <summary>
    /// New access token for the target account.
    /// </summary>
    public required string AccessToken { get; set; }

    /// <summary>
    /// New refresh token (if issued).
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Token expiration time in seconds.
    /// </summary>
    public int ExpiresIn { get; set; }

    /// <summary>
    /// Token type (typically "Bearer").
    /// </summary>
    public required string TokenType { get; set; }

    /// <summary>
    /// Details of the account that was switched to.
    /// </summary>
    public required AccountSummary Account { get; set; }
}

// ============================================
// REFRESH TOKEN
// ============================================

/// <summary>
/// Request to refresh an access token.
/// </summary>
public class RefreshTokenRequest
{
    /// <summary>
    /// The refresh token obtained from a previous token response.
    /// </summary>
    [Required]
    public required string RefreshToken { get; set; }
}

/// <summary>
/// Response containing new tokens after refresh.
/// </summary>
public class TokenResponse
{
    /// <summary>
    /// New access token.
    /// </summary>
    public required string AccessToken { get; set; }

    /// <summary>
    /// New refresh token (token rotation).
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Token expiration time in seconds.
    /// </summary>
    public int ExpiresIn { get; set; }

    /// <summary>
    /// Token type (typically "Bearer").
    /// </summary>
    public required string TokenType { get; set; }
}

// ============================================
// TOKEN EXCHANGE (RFC 8693) - For Documentation
// ============================================

/// <summary>
/// Token Exchange request parameters (RFC 8693).
/// Send to POST /connect/token with Content-Type: application/x-www-form-urlencoded
/// 
/// Example:
/// grant_type=urn:ietf:params:oauth:grant-type:token-exchange
/// &amp;subject_token={current_access_token}
/// &amp;subject_token_type=urn:ietf:params:oauth:token-type:access_token
/// &amp;account_id={target_account_guid}
/// &amp;scope=openid profile account shop.api
/// </summary>
public static class TokenExchangeConstants
{
    public const string GrantType = "urn:ietf:params:oauth:grant-type:token-exchange";
    public const string SubjectTokenType = "urn:ietf:params:oauth:token-type:access_token";
}

