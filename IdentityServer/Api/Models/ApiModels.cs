using System.ComponentModel.DataAnnotations;

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
}

// ============================================
// ME (Self-access)
// ============================================

public class MeResponse
{
    public required string UserId { get; set; }
    public string? Email { get; set; }
    public string? FullName { get; set; }
}

// ============================================
// REFRESH TOKEN
// ============================================

public class RefreshTokenRequest
{
    [Required]
    public required string RefreshToken { get; set; }
}

public class TokenResponse
{
    public required string AccessToken { get; set; }
    public string? RefreshToken { get; set; }
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

