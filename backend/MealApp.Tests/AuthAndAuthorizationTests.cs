using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MealApp.Data;
using MealApp.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MealApp.Tests;

public class AuthAndAuthorizationTests
{
    [Fact]
    public async Task Register_CreatesUserRoleOnly()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = CreateEmail();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "Test User",
            email,
            password = "Pass123$",
            role = "Admin"
        });

        await EnsureSuccessAsync(response);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);

        Assert.NotNull(user);
        Assert.True(await userManager.IsInRoleAsync(user, "User"));
        Assert.False(await userManager.IsInRoleAsync(user, "Admin"));
    }

    [Fact]
    public async Task Login_ReturnsToken()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = CreateEmail();

        await RegisterUserAsync(client, email);

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "Pass123$"
        });

        await EnsureSuccessAsync(response);

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var result = json.RootElement.GetProperty("result");

        Assert.True(json.RootElement.GetProperty("isSuccess").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(result.GetProperty("token").GetString()));
    }

    [Fact]
    public async Task NormalUser_CannotAccessAdminEndpoints()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = CreateEmail();

        await RegisterUserAsync(client, email);
        var token = await LoginAndGetTokenAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/category", new
        {
            name = "Protected Category",
            description = "Should not be created by normal user."
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanCreateCategoryAndRecipe()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = CreateEmail();

        await CreateAdminAsync(factory, email);
        var token = await LoginAndGetTokenAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var categoryResponse = await client.PostAsJsonAsync("/api/category", new
        {
            name = "Admin Test Category",
            description = "Created by admin test."
        });

        Assert.Equal(HttpStatusCode.Created, categoryResponse.StatusCode);

        using var categoryJson = await JsonDocument.ParseAsync(await categoryResponse.Content.ReadAsStreamAsync());
        var categoryId = categoryJson.RootElement
            .GetProperty("result")
            .GetProperty("id")
            .GetInt32();

        var recipeResponse = await client.PostAsJsonAsync("/api/recipe", new
        {
            title = "Admin Test Recipe",
            description = "Created by admin test.",
            imageUrl = "",
            preparationTimeMinutes = 10,
            cookingTimeMinutes = 20,
            servings = 4,
            categoryId
        });

        Assert.Equal(HttpStatusCode.Created, recipeResponse.StatusCode);
    }

    private static async Task RegisterUserAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "Test User",
            email,
            password = "Pass123$"
        });

        await EnsureSuccessAsync(response);
    }

    private static async Task<string> LoginAndGetTokenAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "Pass123$"
        });

        await EnsureSuccessAsync(response);

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        return json.RootElement
            .GetProperty("result")
            .GetProperty("token")
            .GetString()!;
    }

    private static async Task CreateAdminAsync(MealAppApiFactory factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            Name = "Test Admin",
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(user, "Pass123$");

        Assert.True(createResult.Succeeded);

        var roleResult = await userManager.AddToRoleAsync(user, "Admin");

        Assert.True(roleResult.Succeeded);
    }

    private static string CreateEmail()
    {
        return $"test.{Guid.NewGuid():N}@example.com";
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();

        throw new HttpRequestException(
            $"Response status code does not indicate success: {(int)response.StatusCode} ({response.StatusCode}). Body: {body}");
    }
}

public class MealAppApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"MealAppTests-{Guid.NewGuid():N}";

    public MealAppApiFactory()
    {
        Environment.SetEnvironmentVariable("Jwt__Secret", "MealAppIntegrationTestSecretKey-ChangeThis");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "MealApp");
        Environment.SetEnvironmentVariable("Jwt__Audience", "MealAppUsers");
        Environment.SetEnvironmentVariable("AdminUser__Email", string.Empty);
        Environment.SetEnvironmentVariable("AdminUser__Password", string.Empty);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
        });

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "MealAppIntegrationTestSecretKey-ChangeThis",
                ["Jwt:Issuer"] = "MealApp",
                ["Jwt:Audience"] = "MealAppUsers",
                ["AdminUser:Email"] = "",
                ["AdminUser:Password"] = ""
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        RoleSeeder.SeedRolesAsync(scope.ServiceProvider).GetAwaiter().GetResult();

        return host;
    }
}
