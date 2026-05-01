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

    [Fact]
    public async Task User_CanAddExistingIngredientToShoppingList()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = CreateEmail();

        await RegisterUserAsync(client, email);
        var setup = await CreateShoppingListSetupAsync(factory, email);
        var token = await LoginAndGetTokenAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var firstResponse = await client.PostAsJsonAsync($"/api/shoppinglist/{setup.ShoppingListId}/items", new
        {
            ingredientId = setup.IngredientId,
            quantity = 2,
            unitId = setup.UnitId
        });

        await EnsureSuccessAsync(firstResponse);

        var duplicateResponse = await client.PostAsJsonAsync($"/api/shoppinglist/{setup.ShoppingListId}/items", new
        {
            ingredientId = setup.IngredientId,
            quantity = 3,
            unitId = setup.UnitId
        });

        await EnsureSuccessAsync(duplicateResponse);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var items = await context.ShoppingListItems
            .Where(item => item.ShoppingListId == setup.ShoppingListId)
            .ToListAsync();

        Assert.Single(items);
        Assert.Equal(5, items[0].Quantity);
    }

    [Fact]
    public async Task User_CannotAddInvalidIngredientToShoppingList()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = CreateEmail();

        await RegisterUserAsync(client, email);
        var setup = await CreateShoppingListSetupAsync(factory, email);
        var token = await LoginAndGetTokenAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync($"/api/shoppinglist/{setup.ShoppingListId}/items", new
        {
            ingredientId = 999999,
            quantity = 1,
            unitId = setup.UnitId
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedUser_CannotAddShoppingListItem()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = CreateEmail();

        await RegisterUserAsync(client, email);
        var setup = await CreateShoppingListSetupAsync(factory, email);

        var response = await client.PostAsJsonAsync($"/api/shoppinglist/{setup.ShoppingListId}/items", new
        {
            ingredientId = setup.IngredientId,
            quantity = 1,
            unitId = setup.UnitId
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task User_CanDeleteOwnShoppingListItem()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = CreateEmail();

        await RegisterUserAsync(client, email);
        var setup = await CreateShoppingListSetupAsync(factory, email, createItem: true);
        var token = await LoginAndGetTokenAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.DeleteAsync($"/api/shoppinglist/items/{setup.ItemId}");

        await EnsureSuccessAsync(response);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.False(await context.ShoppingListItems.AnyAsync(item => item.Id == setup.ItemId));
    }

    [Fact]
    public async Task User_CannotDeleteAnotherUsersShoppingListItem()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var firstEmail = CreateEmail();
        var secondEmail = CreateEmail();

        await RegisterUserAsync(client, firstEmail);
        await RegisterUserAsync(client, secondEmail);
        var otherSetup = await CreateShoppingListSetupAsync(factory, secondEmail, createItem: true);
        var token = await LoginAndGetTokenAsync(client, firstEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.DeleteAsync($"/api/shoppinglist/items/{otherSetup.ItemId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.True(await context.ShoppingListItems.AnyAsync(item => item.Id == otherSetup.ItemId));
    }

    [Fact]
    public async Task User_CanClearOwnShoppingList()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = CreateEmail();

        await RegisterUserAsync(client, email);
        var setup = await CreateShoppingListSetupAsync(factory, email, createItem: true);
        await AddShoppingListItemAsync(factory, setup.ShoppingListId, setup.IngredientId, setup.UnitId, 4);
        var token = await LoginAndGetTokenAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.DeleteAsync($"/api/shoppinglist/{setup.ShoppingListId}/items");

        await EnsureSuccessAsync(response);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.False(await context.ShoppingListItems.AnyAsync(item => item.ShoppingListId == setup.ShoppingListId));
        Assert.True(await context.Ingredients.AnyAsync(ingredient => ingredient.Id == setup.IngredientId));
    }

    [Fact]
    public async Task ClearingShoppingList_DoesNotAffectOtherUsers()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var firstEmail = CreateEmail();
        var secondEmail = CreateEmail();

        await RegisterUserAsync(client, firstEmail);
        await RegisterUserAsync(client, secondEmail);
        var ownSetup = await CreateShoppingListSetupAsync(factory, firstEmail, createItem: true);
        var otherSetup = await CreateShoppingListSetupAsync(factory, secondEmail, createItem: true);
        var token = await LoginAndGetTokenAsync(client, firstEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.DeleteAsync($"/api/shoppinglist/{ownSetup.ShoppingListId}/items");

        await EnsureSuccessAsync(response);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.False(await context.ShoppingListItems.AnyAsync(item => item.ShoppingListId == ownSetup.ShoppingListId));
        Assert.True(await context.ShoppingListItems.AnyAsync(item => item.ShoppingListId == otherSetup.ShoppingListId));
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

    private static async Task<ShoppingListSetup> CreateShoppingListSetupAsync(
        MealAppApiFactory factory,
        string email,
        bool createItem = false)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);

        Assert.NotNull(user);

        var ingredientCategory = new IngredientCategory
        {
            Name = $"Test Category {Guid.NewGuid():N}"
        };
        var ingredient = new Ingredient
        {
            Name = $"Test Ingredient {Guid.NewGuid():N}",
            IngredientCategory = ingredientCategory
        };
        var unit = new Unit
        {
            Name = $"Test Unit {Guid.NewGuid():N}",
            ShortName = "tu"
        };
        var shoppingList = new ShoppingList
        {
            UserId = user.Id,
            Name = "Test Shopping List",
            CreatedAt = DateTime.UtcNow
        };

        context.IngredientCategories.Add(ingredientCategory);
        context.Ingredients.Add(ingredient);
        context.Units.Add(unit);
        context.ShoppingLists.Add(shoppingList);
        await context.SaveChangesAsync();

        var itemId = 0;

        if (createItem)
        {
            itemId = await AddShoppingListItemAsync(
                factory,
                shoppingList.Id,
                ingredient.Id,
                unit.Id,
                2);
        }

        return new ShoppingListSetup(
            shoppingList.Id,
            ingredient.Id,
            unit.Id,
            itemId);
    }

    private static async Task<int> AddShoppingListItemAsync(
        MealAppApiFactory factory,
        int shoppingListId,
        int ingredientId,
        int unitId,
        decimal quantity)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = new ShoppingListItem
        {
            ShoppingListId = shoppingListId,
            IngredientId = ingredientId,
            UnitId = unitId,
            Quantity = quantity,
            IsChecked = false
        };

        context.ShoppingListItems.Add(item);
        await context.SaveChangesAsync();

        return item.Id;
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

public record ShoppingListSetup(
    int ShoppingListId,
    int IngredientId,
    int UnitId,
    int ItemId);

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
