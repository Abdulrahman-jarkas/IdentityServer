using IdentityServer.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityServer.Api.Controllers;

/// <summary>
/// Base controller for API endpoints.
/// Requires Bearer token authentication (access token issued by IdentityServer).
/// </summary>
[ApiController]
[Authorize(AuthenticationSchemes = "IdentityServerAccessToken")]
[Route("api/[controller]")]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected ActionResult Forbidden(string message) =>
        StatusCode(403, new ApiError { Error = "Forbidden", Detail = message });

    protected ActionResult NotFoundError(string message) =>
        NotFound(new ApiError { Error = "Not Found", Detail = message });

    protected ActionResult BadRequestError(string message) =>
        BadRequest(new ApiError { Error = "Bad Request", Detail = message });

    protected ActionResult Conflict(string message) =>
        StatusCode(409, new ApiError { Error = "Conflict", Detail = message });

    /// <summary>
    /// Current user's ID from the sub claim.
    /// </summary>
    protected string? CurrentUserId => User.FindFirst("sub")?.Value;
}
