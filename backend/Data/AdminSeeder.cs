using MealApp.Models;
using Microsoft.AspNetCore.Identity;

namespace MealApp.Data;

public static class AdminSeeder
{
    private const string AdminFirstName = "Ammar";
    private const string AdminLastName = "Admin";
    private const string AdminRole = "Admin";

    public static async Task SeedDefaultAdminAsync(IServiceProvider serviceProvider)
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var email = configuration["AdminUser:Email"];
        var password = configuration["AdminUser:Password"];

        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        if (!await roleManager.RoleExistsAsync(AdminRole))
        {
            var roleResult = await roleManager.CreateAsync(new IdentityRole(AdminRole));

            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to create {AdminRole} role: {FormatErrors(roleResult)}");
            }
        }

        var adminUser = await userManager.FindByEmailAsync(email);

        if (adminUser is null)
        {
            adminUser = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = AdminFirstName,
                LastName = AdminLastName,
                Name = $"{AdminFirstName} {AdminLastName}",
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(adminUser, password);

            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to create default admin user: {FormatErrors(createResult)}");
            }
        }

        if (!await userManager.IsInRoleAsync(adminUser, AdminRole))
        {
            var addToRoleResult = await userManager.AddToRoleAsync(adminUser, AdminRole);

            if (!addToRoleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to assign {AdminRole} role to default admin user: {FormatErrors(addToRoleResult)}");
            }
        }
    }

    private static string FormatErrors(IdentityResult result)
    {
        return string.Join("; ", result.Errors.Select(error => error.Description));
    }
}
