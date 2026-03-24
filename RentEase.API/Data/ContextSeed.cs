using Microsoft.AspNetCore.Identity;

namespace PropertyLeasing.API.Data;

public static class ContextSeed
{
    public static async Task SeedRolesAndUsersAsync(IServiceProvider serviceProvider)
    {
        try
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<AppUser>>();

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

            await SeedUser(userManager, "manager@propleasing.com", "Ahmed Al Mansoori", "Manager@123", "PropertyManager");
            await SeedUser(userManager, "tenant1@example.com", "Sara Al Khalifa", "Tenant@123", "Tenant");
            await SeedUser(userManager, "staff1@propleasing.com", "Ali Hassan", "Staff@123", "MaintenanceStaff");
        }
        catch { }
    }

    private static async Task SeedUser(UserManager<AppUser> userManager, string email, string fullName, string password, string role)
    {
        try
        {
            var existing = await userManager.FindByEmailAsync(email);
            if (existing != null) return;

            var user = new AppUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(user, password);
            if (result.Succeeded)
                await userManager.AddToRoleAsync(user, role);
        }
        catch { }
    }
}