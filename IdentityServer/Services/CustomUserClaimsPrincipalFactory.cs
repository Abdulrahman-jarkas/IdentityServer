using System.Security.Claims;
using IdentityServer.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace IdentityServer.Services;

/// <summary>
/// Custom claims principal factory that adds basic user claims to the identity.
/// This is called during sign-in to populate the authentication cookie for IdentityServer UI.
/// 
/// Note: Account-related claims are handled by CustomProfileService during token issuance.
/// </summary>
public class CustomUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    public CustomUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        // Add basic user profile claims
        if (!string.IsNullOrEmpty(user.FirstName))
        {
            identity.AddClaim(new Claim("given_name", user.FirstName));
        }
        if (!string.IsNullOrEmpty(user.LastName))
        {
            identity.AddClaim(new Claim("family_name", user.LastName));
        }
        if (!string.IsNullOrEmpty(user.FullName))
        {
            identity.AddClaim(new Claim("name", user.FullName));
        }

        return identity;
    }
}
