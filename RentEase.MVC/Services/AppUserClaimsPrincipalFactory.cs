using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PropertyLeasing.API.Data;
using PropertyLeasing.BusinessLogic;

namespace PropertyLeasing.MVC.Services;

/// <summary>Adds <see cref="TenantProfileSync.DisplayNameClaimType"/> and sets <see cref="ClaimTypes.Name"/> to the user's full name.</summary>
public class AppUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<AppUser, IdentityRole>
{
    public AppUserClaimsPrincipalFactory(
        UserManager<AppUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        var displayName = TenantProfileSync.ResolveNavbarDisplayName(
            user.FullName, user.Email, user.UserName);

        ReplaceClaim(identity, ClaimTypes.Name, displayName);
        ReplaceClaim(identity, TenantProfileSync.DisplayNameClaimType, displayName);

        return identity;
    }

    private static void ReplaceClaim(ClaimsIdentity identity, string claimType, string value)
    {
        var existing = identity.FindFirst(claimType);
        if (existing != null)
            identity.RemoveClaim(existing);
        identity.AddClaim(new Claim(claimType, value));
    }
}
