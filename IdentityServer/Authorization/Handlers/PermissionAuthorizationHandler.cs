using IdentityServer.Authorization.Requirements;
using Microsoft.AspNetCore.Authorization;

namespace IdentityServer.Authorization.Handlers;

/// <summary>
/// Handles permission-based authorization by checking the user's permission claims.
/// No bypassing - each permission must be explicitly granted.
/// </summary>
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly ILogger<PermissionAuthorizationHandler> _logger;

    public PermissionAuthorizationHandler(ILogger<PermissionAuthorizationHandler> logger)
    {
        _logger = logger;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var userId = context.User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogDebug("No user id found in claims");
            return Task.CompletedTask;
        }

        // Get all permission claims
        var permissions = context.User.FindAll("permission")
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Check if user has the exact required permission
        if (permissions.Contains(requirement.Permission))
        {
            _logger.LogDebug("User {UserId} has required permission {Permission}", 
                userId, requirement.Permission);
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        _logger.LogDebug("User {UserId} does not have required permission {Permission}. User permissions: {Permissions}", 
            userId, requirement.Permission, string.Join(", ", permissions));

        return Task.CompletedTask;
    }
}
