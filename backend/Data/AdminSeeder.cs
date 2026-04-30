using MealApp.Models;
using Microsoft.AspNetCore.Identity;

namespace MealApp.Data;

public static class AdminSeeder
{
    private const string AdminEmail = "amfo250@gmail.com";
    private const string AdminName = "Ammar";
    private const string AdminRole = "Admin";

    public static async Task SeedDefaultAdminAsync(IServiceProvider serviceProvider)
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var password = configuration["AdminUser:Password"];

        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        if (!await roleManager.RoleExistsAsync(AdminRole))
        {
            await roleManager.CreateAsync(new IdentityRole(AdminRole));
        }

        var adminUser = await userManager.FindByEmailAsync(AdminEmail);

        if (adminUser is null)
        {
            adminUser = new ApplicationUser
            {
                UserName = AdminEmail,
                Email = AdminEmail,
                Name = AdminName,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(adminUser, password);

            if (!createResult.Succeeded)
            {
                return;
            }
        }

        if (!await userManager.IsInRoleAsync(adminUser, AdminRole))
        {
            await userManager.AddToRoleAsync(adminUser, AdminRole);
        }
    }
}
