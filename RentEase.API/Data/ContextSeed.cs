using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PropertyLeasing.API.Models;

namespace PropertyLeasing.API.Data;

public static class ContextSeed
{
    public static async Task SeedRolesAndUsersAsync(IServiceProvider serviceProvider)
    {
        try
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<AppUser>>();
            var appDb       = serviceProvider.GetRequiredService<PropertyLeasingDbContext>();

            string[] roles = { "PropertyManager", "Tenant", "MaintenanceStaff" };
            foreach (var role in roles)
            {
                try
                {
                    if (!await roleManager.RoleExistsAsync(role))
                        await roleManager.CreateAsync(new IdentityRole(role));
                }
                catch { }
            }

            await SeedUser(userManager, appDb, "manager@propleasing.com", "Ahmed Al Mansoori", "Manager@123", "PropertyManager");
            await SeedUser(userManager, appDb, "tenant1@example.com", "Sara Al Khalifa", "Tenant@123", "Tenant");
            await SeedUser(userManager, appDb, "staff1@propleasing.com", "Ali Hassan", "Staff@123", "MaintenanceStaff");
        }
        catch { }
    }

    private static async Task SeedUser(UserManager<AppUser> userManager, PropertyLeasingDbContext appDb, string email, string fullName, string password, string role)
    {
        try
        {
            var existing = await userManager.FindByEmailAsync(email);
            if (existing == null)
            {
                var user = new AppUser
                {
                    UserName = email,
                    Email = email,
                    FullName = fullName,
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(user, password);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, role);
                    existing = user;
                }
            }

            if (existing == null) return;

            // Ensure a matching app User record exists
            var alreadyLinked = await appDb.Users.AnyAsync(u => u.IdentityUserId == existing.Id);
            if (!alreadyLinked)
            {
                appDb.Users.Add(new User
                {
                    FullName       = fullName,
                    Email          = email,
                    Role           = role,
                    IdentityUserId = existing.Id
                });
                await appDb.SaveChangesAsync();
            }
        }
        catch { }
    }
}