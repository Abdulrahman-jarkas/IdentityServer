using IdentityServer.Authorization.Requirements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace IdentityServer.Authorization;

/// <summary>
/// Attribute to require a specific permission for an action or controller.
/// Usage: [RequirePermission(Permissions.Shop.ManageAccounts)]
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permission)
    {
        Policy = $"Permission:{permission}";
    }
}

/// <summary>
/// Extension methods to register dynamic permission policies.
/// </summary>
public static class PermissionPolicyExtensions
{
    /// <summary>
    /// Adds a policy provider that dynamically creates policies for permissions.
    /// This allows using [RequirePermission("shop.accounts.manage")] without pre-registering each policy.
    /// </summary>
    public static IServiceCollection AddPermissionPolicies(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        return services;
    }
}

/// <summary>
/// Policy provider that creates permission policies on-demand.
/// </summary>
public class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private const string PolicyPrefix = "Permission:";
    private readonly DefaultAuthorizationPolicyProvider _fallbackPolicyProvider;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallbackPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => 
        _fallbackPolicyProvider.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => 
        _fallbackPolicyProvider.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(PolicyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var permission = policyName[PolicyPrefix.Length..];
            var policy = new AuthorizationPolicyBuilder()
                .AddRequirements(new PermissionRequirement(permission))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallbackPolicyProvider.GetPolicyAsync(policyName);
    }
}
