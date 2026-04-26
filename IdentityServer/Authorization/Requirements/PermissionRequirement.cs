namespace IdentityServer.Authorization.Requirements;

/// <summary>
/// Authorization requirement that checks if the user has a specific permission.
/// </summary>
public class PermissionRequirement : Microsoft.AspNetCore.Authorization.IAuthorizationRequirement
{
    public string Permission { get; }

    public PermissionRequirement(string permission)
    {
        Permission = permission ?? throw new ArgumentNullException(nameof(permission));
    }
}
