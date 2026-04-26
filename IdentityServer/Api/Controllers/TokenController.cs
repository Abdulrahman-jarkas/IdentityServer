using IdentityServer.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace IdentityServer.Api.Controllers;

/// <summary>
/// Token management endpoints for refreshing and managing access tokens.
/// These endpoints help clients (like Swagger) manage token lifecycle.
/// </summary>
[ApiController]
[Route("api/token")]
[Produces("application/json")]
public class TokenController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public TokenController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Refresh an access token using a refresh token.
    /// Use this when your access token expires.
    /// </summary>
    /// <param name="request">The refresh token request</param>
    /// <returns>New access token and refresh token</returns>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TokenResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        if (string.IsNullOrEmpty(request.RefreshToken))
            return BadRequest(new ApiError { Error = "Bad Request", Detail = "Refresh token is required." });

        // Call the token endpoint with refresh_token grant
        using var httpClient = new HttpClient();
        var tokenEndpoint = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/connect/token";

        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = request.RefreshToken,
            ["client_id"] = "swagger.ui"
        });

        var response = await httpClient.PostAsync(tokenEndpoint, tokenRequest);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var errorResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
            var errorDescription = errorResponse.TryGetProperty("error_description", out var desc)
                ? desc.GetString()
                : "Token refresh failed";
            var error = errorResponse.TryGetProperty("error", out var err)
                ? err.GetString()
                : "invalid_grant";

            return BadRequest(new ApiError 
            { 
                Error = error ?? "invalid_grant", 
                Detail = errorDescription ?? "Token refresh failed" 
            });
        }

        var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

        return Ok(new TokenResponse
        {
            AccessToken = tokenResponse.GetProperty("access_token").GetString()!,
            RefreshToken = tokenResponse.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null,
            ExpiresIn = tokenResponse.GetProperty("expires_in").GetInt32(),
            TokenType = tokenResponse.GetProperty("token_type").GetString()!
        });
    }

    /// <summary>
    /// Get information about token endpoints and grant types supported.
    /// Useful for clients to discover available token operations.
    /// </summary>
    [HttpGet("info")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenEndpointInfo), StatusCodes.Status200OK)]
    public ActionResult<TokenEndpointInfo> GetTokenInfo()
    {
        var authority = _configuration["IdentityServer:Authority"] ?? $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}";

        return Ok(new TokenEndpointInfo
        {
            TokenEndpoint = $"{authority}/connect/token",
            AuthorizeEndpoint = $"{authority}/connect/authorize",
            SupportedGrantTypes = new[]
            {
                "authorization_code",
                "refresh_token",
                "urn:ietf:params:oauth:grant-type:token-exchange"
            },
            RefreshTokenUsage = "Use POST /api/token/refresh with your refresh_token to get a new access_token",
            SwitchAccountUsage = "Use POST /api/me/switch-account with your current Bearer token to switch accounts"
        });
    }
}

/// <summary>
/// Information about available token endpoints and operations.
/// </summary>
public class TokenEndpointInfo
{
    public required string TokenEndpoint { get; set; }
    public required string AuthorizeEndpoint { get; set; }
    public required string[] SupportedGrantTypes { get; set; }
    public required string RefreshTokenUsage { get; set; }
    public required string SwitchAccountUsage { get; set; }
}
