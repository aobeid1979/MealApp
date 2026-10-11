using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MealApp.Data;
using MealApp.Models;
using Microsoft.AspNetCore.DataProtection;
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
            firstName = "Test",
            lastName = "User",
            email,
            password = "Pass123$",
            role = "Admin"
        });

        await EnsureSuccessAsync(response);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);

        Assert.NotNull(user);
        Assert.True(user.EmailConfirmed);
        Assert.True(await userManager.IsInRoleAsync(user, "User"));
        Assert.False(await userManager.IsInRoleAsync(user, "Admin"));
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsClearBadRequest()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = CreateEmail();

        await RegisterUserAsync(factory, client, email);

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            firstName = "Another",
            lastName = "User",
            email,
            password = "Pass123$"
        });

        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Email is already registered.", content);
    }

    [Fact]
    public async Task Register_AllowsImmediateLoginWhileEmailVerificationIsDisabled()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = CreateEmail();

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            firstName = "Test",
            lastName = "User",
            email,
            password = "Pass123$"
        });

        await EnsureSuccessAsync(registerResponse);

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "Pass123$"
        });

        await EnsureSuccessAsync(response);
    }

    [Fact]
    public async Task Login_ReturnsToken()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = CreateEmail();

        await RegisterUserAsync(factory, client, email);

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
    public async Task Login_WithIncorrectPassword_ReturnsInvalidCredentialsApiResponse()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = CreateEmail();

        await RegisterUserAsync(factory, client, email);

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "WrongPass123$"
        });

        await AssertInvalidLoginResponseAsync(response);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsInvalidCredentialsApiResponse()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = CreateEmail(),
            password = "Pass123$"
        });

        await AssertInvalidLoginResponseAsync(response);
    }

    [Fact]
    public async Task Login_WithSurroundingEmailWhitespace_ReturnsToken()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = CreateEmail();

        await RegisterUserAsync(factory, client, email);

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = $"  {email}  ",
            password = "Pass123$"
        });

        await EnsureSuccessAsync(response);

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var result = json.RootElement.GetProperty("result");

        Assert.True(json.RootElement.GetProperty("isSuccess").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(result.GetProperty("token").GetString()));
        Assert.Equal(email, result.GetProperty("email").GetString());
        Assert.False(json.RootElement.TryGetProperty("token", out _));
    }

    [Fact]
    public async Task FailedLogin_ReturnsExpectedApiErrorFormat()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = CreateEmail(),
            password = "Pass123$"
        });

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = json.RootElement;

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(root.GetProperty("isSuccess").GetBoolean());
        Assert.Equal(401, root.GetProperty("statusCode").GetInt32());
        Assert.Equal(
            "Invalid email or password.",
            root.GetProperty("errorMessages")[0].GetString());
        Assert.True(root.TryGetProperty("result", out _));
    }

    [Fact]
    public async Task NormalUser_CannotAccessAdminEndpoints()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = CreateEmail();

        await RegisterUserAsync(factory, client, email);
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
    public async Task Admin_CanDeleteUnusedCategory()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = CreateEmail();

        await CreateAdminAsync(factory, email);
        var categoryId = await CreateCategoryAsync(factory);
        var token = await LoginAndGetTokenAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.DeleteAsync($"/api/category/{categoryId}");

        await EnsureSuccessAsync(response);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.False(await context.Categories.AnyAsync(category => category.Id == categoryId));
    }

    [Fact]
    public async Task Admin_CannotDeleteCategoryUsedByOneRecipe()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = CreateEmail();

        await CreateAdminAsync(factory, email);
        var setup = await CreateCategoryWithRecipesAsync(factory, recipeCount: 1);
        var token = await LoginAndGetTokenAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.DeleteAsync($"/api/category/{setup.CategoryId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = json.RootElement;

        Assert.False(root.GetProperty("isSuccess").GetBoolean());
        Assert.Equal(409, root.GetProperty("statusCode").GetInt32());
        Assert.Equal(
            "Cannot delete category because it is used by existing recipes.",
            root.GetProperty("errorMessages")[0].GetString());
        Assert.Equal(1, root.GetProperty("result").GetProperty("recipeCount").GetInt32());

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.True(await context.Categories.AnyAsync(category => category.Id == setup.CategoryId));
        Assert.Equal(1, await context.Recipes.CountAsync(recipe => recipe.CategoryId == setup.CategoryId));
    }

    [Fact]
    public async Task Admin_CannotDeleteCategoryUsedByMultipleRecipes()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = CreateEmail();

        await CreateAdminAsync(factory, email);
        var setup = await CreateCategoryWithRecipesAsync(factory, recipeCount: 3);
        var token = await LoginAndGetTokenAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.DeleteAsync($"/api/category/{setup.CategoryId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = json.RootElement;

        Assert.Equal(3, root.GetProperty("result").GetProperty("recipeCount").GetInt32());

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Equal(3, await context.Recipes.CountAsync(recipe => recipe.CategoryId == setup.CategoryId));
    }

    [Fact]
    public async Task Admin_DeleteCategoryWithInvalidIdReturnsNotFound()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = CreateEmail();

        await CreateAdminAsync(factory, email);
        var token = await LoginAndGetTokenAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.DeleteAsync("/api/category/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task User_CanAddExistingIngredientToShoppingList()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = CreateEmail();

        await RegisterUserAsync(factory, client, email);
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
    public async Task User_CanAddCustomShoppingListItem()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = CreateEmail();

        await RegisterUserAsync(factory, client, email);
        var setup = await CreateShoppingListSetupAsync(factory, email);
        var token = await LoginAndGetTokenAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync($"/api/shoppinglist/{setup.ShoppingListId}/items", new
        {
            customName = "Toothpaste"
        });

        await EnsureSuccessAsync(response);

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var result = json.RootElement.GetProperty("result");

        Assert.Equal("Toothpaste", result.GetProperty("customName").GetString());
        Assert.Equal("Toothpaste", result.GetProperty("itemName").GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("ingredientId").ValueKind);
        Assert.Equal(JsonValueKind.Null, result.GetProperty("quantity").ValueKind);
        Assert.False(result.GetProperty("isGenerated").GetBoolean());
    }

    [Fact]
    public async Task User_CannotAddBlankCustomShoppingListItem()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = CreateEmail();

        await RegisterUserAsync(factory, client, email);
        var setup = await CreateShoppingListSetupAsync(factory, email);
        var token = await LoginAndGetTokenAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync($"/api/shoppinglist/{setup.ShoppingListId}/items", new
        {
            customName = " "
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task User_CannotAddIngredientAndCustomNameInSameShoppingListItem()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = CreateEmail();

        await RegisterUserAsync(factory, client, email);
        var setup = await CreateShoppingListSetupAsync(factory, email);
        var token = await LoginAndGetTokenAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync($"/api/shoppinglist/{setup.ShoppingListId}/items", new
        {
            ingredientId = setup.IngredientId,
            customName = "Custom chicken",
            quantity = 1,
            unitId = setup.UnitId
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task User_CanMergeDuplicateCustomShoppingListItems()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = CreateEmail();

        await RegisterUserAsync(factory, client, email);
        var setup = await CreateShoppingListSetupAsync(factory, email);
        var token = await LoginAndGetTokenAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await EnsureSuccessAsync(await client.PostAsJsonAsync($"/api/shoppinglist/{setup.ShoppingListId}/items", new
        {
            customName = "Bananas",
            quantity = 2,
            unitId = setup.UnitId
        }));
        await EnsureSuccessAsync(await client.PostAsJsonAsync($"/api/shoppinglist/{setup.ShoppingListId}/items", new
        {
            customName = "bananas",
            quantity = 3,
            unitId = setup.UnitId
        }));

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = await context.ShoppingListItems
            .SingleAsync(item => item.ShoppingListId == setup.ShoppingListId
                && item.CustomName == "Bananas"
                && item.IngredientId == null);

        Assert.Equal(5, item.Quantity);
        Assert.Equal(setup.UnitId, item.UnitId);
        Assert.False(item.IsGenerated);
    }

    [Fact]
    public async Task GenerateShoppingList_PreservesCustomManualItems()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = CreateEmail();

        await RegisterUserAsync(factory, client, email);
        var setup = await CreateMealPlanShoppingListMergeSetupAsync(factory, email);
        var token = await LoginAndGetTokenAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await EnsureSuccessAsync(await client.PostAsJsonAsync($"/api/shoppinglist/{setup.ShoppingListId}/items", new
        {
            customName = "Paper towels"
        }));
        await EnsureSuccessAsync(await client.PostAsync("/api/shoppinglist/generate", null));
        await EnsureSuccessAsync(await client.PostAsync("/api/shoppinglist/generate", null));

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var items = await context.ShoppingListItems
            .Where(item => item.ShoppingListId == setup.ShoppingListId)
            .ToListAsync();

        var customItem = Assert.Single(items, item => item.CustomName == "Paper towels");
        Assert.False(customItem.IsGenerated);
        Assert.Null(customItem.IngredientId);

        var generatedItem = Assert.Single(items, item => item.IsGenerated == true);
        Assert.Equal(setup.IngredientId, generatedItem.IngredientId);
        Assert.Equal(3, generatedItem.Quantity);
    }

    [Fact]
    public async Task User_CanToggleCustomShoppingListItem()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = CreateEmail();

        await RegisterUserAsync(factory, client, email);
        var setup = await CreateShoppingListSetupAsync(factory, email);
        var itemId = await AddCustomShoppingListItemAsync(factory, setup.ShoppingListId, "Dishwasher tablets");
        var token = await LoginAndGetTokenAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PatchAsync($"/api/shoppinglist/items/{itemId}/toggle", null);

        await EnsureSuccessAsync(response);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = await context.ShoppingListItems.SingleAsync(item => item.Id == itemId);

        Assert.True(item.IsChecked);
    }

    [Fact]
    public async Task User_CannotUpdateAnotherUsersCustomShoppingListItem()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var firstEmail = CreateEmail();
        var secondEmail = CreateEmail();

        await RegisterUserAsync(factory, client, firstEmail);
        await RegisterUserAsync(factory, client, secondEmail);
        var otherSetup = await CreateShoppingListSetupAsync(factory, secondEmail);
        var otherItemId = await AddCustomShoppingListItemAsync(factory, otherSetup.ShoppingListId, "Toothpaste", 1);
        var token = await LoginAndGetTokenAsync(client, firstEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PutAsJsonAsync($"/api/shoppinglist/items/{otherItemId}", new
        {
            quantity = 2
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = await context.ShoppingListItems.SingleAsync(item => item.Id == otherItemId);

        Assert.Equal(1, item.Quantity);
    }

    [Fact]
    public async Task GenerateShoppingList_PreservesManualItemWithSameIngredientAndUnit()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = CreateEmail();

        await RegisterUserAsync(factory, client, email);
        var setup = await CreateMealPlanShoppingListMergeSetupAsync(factory, email, includeManualItem: true);
        var token = await LoginAndGetTokenAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync("/api/shoppinglist/generate", null);

        await EnsureSuccessAsync(response);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var items = await context.ShoppingListItems
            .Where(item => item.ShoppingListId == setup.ShoppingListId)
            .ToListAsync();

        Assert.Equal(2, items.Count);

        var manualItem = Assert.Single(items, item => item.IsGenerated == false);
        Assert.Equal(setup.IngredientId, manualItem.IngredientId);
        Assert.Equal(setup.UnitId, manualItem.UnitId);
        Assert.Equal(2, manualItem.Quantity);
        Assert.True(manualItem.IsChecked);

        var generatedItem = Assert.Single(items, item => item.IsGenerated == true);
        Assert.Equal(setup.IngredientId, generatedItem.IngredientId);
        Assert.Equal(setup.UnitId, generatedItem.UnitId);
        Assert.Equal(3, generatedItem.Quantity);
        Assert.False(generatedItem.IsChecked);
    }

    [Fact]
    public async Task GenerateShoppingList_IsIdempotent_WhenRunRepeatedly()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = CreateEmail();

        await RegisterUserAsync(factory, client, email);
        var setup = await CreateMealPlanShoppingListMergeSetupAsync(factory, email);
        var token = await LoginAndGetTokenAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await EnsureSuccessAsync(await client.PostAsync("/api/shoppinglist/generate", null));
        await EnsureSuccessAsync(await client.PostAsync("/api/shoppinglist/generate", null));
        await EnsureSuccessAsync(await client.PostAsync("/api/shoppinglist/generate", null));

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var generatedItem = await context.ShoppingListItems
            .SingleAsync(item => item.ShoppingListId == setup.ShoppingListId && item.IsGenerated == true);

        Assert.Equal(setup.IngredientId, generatedItem.IngredientId);
        Assert.Equal(setup.UnitId, generatedItem.UnitId);
        Assert.Equal(3, generatedItem.Quantity);
    }

    [Fact]
    public async Task GenerateShoppingList_ReusesLegacyMatchingItemWithoutCreatingDuplicate()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = CreateEmail();

        await RegisterUserAsync(factory, client, email);
        var setup = await CreateMealPlanShoppingListMergeSetupAsync(
            factory,
            email,
            includeLegacyItem: true);
        var token = await LoginAndGetTokenAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync("/api/shoppinglist/generate", null);

        await EnsureSuccessAsync(response);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var items = await context.ShoppingListItems
            .Where(item => item.ShoppingListId == setup.ShoppingListId)
            .ToListAsync();

        var item = Assert.Single(items);
        Assert.Equal(setup.ItemId, item.Id);
        Assert.True(item.IsGenerated);
        Assert.Equal(3, item.Quantity);
    }

    [Fact]
    public async Task GenerateShoppingList_CombinesSameIngredientAcrossRecipes()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = CreateEmail();

        await RegisterUserAsync(factory, client, email);
        var setup = await CreateTwoRecipeMealPlanSetupAsync(factory, email);
        var token = await LoginAndGetTokenAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync("/api/shoppinglist/generate", null);

        await EnsureSuccessAsync(response);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var generatedItem = await context.ShoppingListItems
            .SingleAsync(item => item.ShoppingListId == setup.ShoppingListId && item.IsGenerated == true);

        Assert.Equal(setup.IngredientId, generatedItem.IngredientId);
        Assert.Equal(setup.UnitId, generatedItem.UnitId);
        Assert.Equal(5, generatedItem.Quantity);
    }

    [Fact]
    public async Task GenerateShoppingList_UpdatesGeneratedQuantityWhenMealPlanChanges()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = CreateEmail();

        await RegisterUserAsync(factory, client, email);
        var setup = await CreateMealPlanShoppingListMergeSetupAsync(factory, email);
        var token = await LoginAndGetTokenAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await EnsureSuccessAsync(await client.PostAsync("/api/shoppinglist/generate", null));

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var mealPlan = await context.MealPlans.SingleAsync(mealPlan => mealPlan.Id == setup.MealPlanId);
            mealPlan.Servings = 1;
            await context.SaveChangesAsync();
        }

        await EnsureSuccessAsync(await client.PostAsync("/api/shoppinglist/generate", null));

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var generatedItem = await context.ShoppingListItems
                .SingleAsync(item => item.ShoppingListId == setup.ShoppingListId && item.IsGenerated == true);

            Assert.Equal(1.5m, generatedItem.Quantity);
        }
    }

    [Fact]
    public async Task GenerateShoppingList_RemovesGeneratedItemsWhenMealPlansAreRemoved()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = CreateEmail();

        await RegisterUserAsync(factory, client, email);
        var setup = await CreateMealPlanShoppingListMergeSetupAsync(factory, email, includeManualItem: true);
        var token = await LoginAndGetTokenAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await EnsureSuccessAsync(await client.PostAsync("/api/shoppinglist/generate", null));

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var mealPlans = await context.MealPlans
                .Where(mealPlan => mealPlan.Id == setup.MealPlanId)
                .ToListAsync();
            context.MealPlans.RemoveRange(mealPlans);
            await context.SaveChangesAsync();
        }

        await EnsureSuccessAsync(await client.PostAsync("/api/shoppinglist/generate", null));

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var items = await context.ShoppingListItems
                .Where(item => item.ShoppingListId == setup.ShoppingListId)
                .ToListAsync();

            var manualItem = Assert.Single(items);
            Assert.False(manualItem.IsGenerated == true);
            Assert.Equal(2, manualItem.Quantity);
        }
    }

    [Fact]
    public async Task GetMyShoppingLists_ReturnsOnlyActiveShoppingList()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = CreateEmail();

        await RegisterUserAsync(factory, client, email);
        var firstSetup = await CreateShoppingListSetupAsync(factory, email);
        await CreateShoppingListSetupAsync(factory, email);
        var token = await LoginAndGetTokenAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/shoppinglist/my");

        await EnsureSuccessAsync(response);

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var lists = json.RootElement.GetProperty("result").EnumerateArray().ToList();

        var list = Assert.Single(lists);
        Assert.Equal(firstSetup.ShoppingListId, list.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task User_CannotAddInvalidIngredientToShoppingList()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = CreateEmail();

        await RegisterUserAsync(factory, client, email);
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

        await RegisterUserAsync(factory, client, email);
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

        await RegisterUserAsync(factory, client, email);
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

        await RegisterUserAsync(factory, client, firstEmail);
        await RegisterUserAsync(factory, client, secondEmail);
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

        await RegisterUserAsync(factory, client, email);
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

        await RegisterUserAsync(factory, client, firstEmail);
        await RegisterUserAsync(factory, client, secondEmail);
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

    [Fact]
    public async Task GuestSupportRequest_WithEmail_ReturnsServiceUnavailable()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        using var content = CreateSupportFormContent(
            title: "Need help",
            description: "I need help with my shopping list.",
            email: "guest@example.com");

        var response = await client.PostAsync("/api/support", content);

        await AssertSupportUnavailableAsync(response);
    }

    [Fact]
    public async Task GuestSupportRequest_WithoutEmailOrPhone_ReturnsBadRequest()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        using var content = CreateSupportFormContent(
            title: "Need help",
            description: "I need help with my shopping list.");

        var response = await client.PostAsync("/api/support", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Email or phone number is required.", body);
    }

    [Fact]
    public async Task AuthenticatedUserSupportRequest_WithoutGuestContact_ReturnsServiceUnavailable()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        var email = CreateEmail();

        await RegisterUserAsync(factory, client, email);
        var token = await LoginAndGetTokenAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var content = CreateSupportFormContent(
            title: "Account issue",
            description: "I need help with my account.",
            name: "Not the account owner",
            email: "invalid-guest-email");

        var response = await client.PostAsync("/api/support", content);

        await AssertSupportUnavailableAsync(response);
    }

    [Fact]
    public async Task SupportRequest_WithUnsupportedImage_ReturnsBadRequest()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        using var content = CreateSupportFormContent(
            title: "Need help",
            description: "I need help with my shopping list.",
            email: "guest@example.com",
            imageContentType: "text/plain");

        var response = await client.PostAsync("/api/support", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Image must be a JPG, PNG, or WEBP file.", body);
    }

    [Fact]
    public async Task GuestSupportRequest_WithPhoneOnly_ReturnsServiceUnavailable()
    {
        await using var factory = new MealAppApiFactory();
        var client = factory.CreateClient();
        using var content = CreateSupportFormContent(
            title: "Need help",
            description: "I need help with my shopping list.",
            phone: "+46700000000");

        var response = await client.PostAsync("/api/support", content);

        await AssertSupportUnavailableAsync(response);
    }

    private static async Task AssertSupportUnavailableAsync(HttpResponseMessage response)
    {
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = json.RootElement;

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.False(root.GetProperty("isSuccess").GetBoolean());
        Assert.Equal(503, root.GetProperty("statusCode").GetInt32());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("result").ValueKind);
        Assert.Equal(
            "Support email delivery is temporarily unavailable. Please try again later.",
            root.GetProperty("errorMessages")[0].GetString());
    }

    [Theory]
    [InlineData("", "Description", "guest@example.com")]
    [InlineData("Title", "", "guest@example.com")]
    [InlineData("Title", "Description", "invalid-email")]
    public async Task SupportRequest_InvalidInputStillReturnsBadRequest(string title, string description, string email)
    {
        await using var factory = new MealAppApiFactory();
        using var client = factory.CreateClient();
        using var content = CreateSupportFormContent(title, description, email: email);
        using var response = await client.PostAsync("/api/support", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("image/jpeg", "test.jpg")]
    [InlineData("image/png", "test.png")]
    [InlineData("image/webp", "test.webp")]
    public async Task SupportRequest_ValidImageReturnsServiceUnavailable(string contentType, string fileName)
    {
        await using var factory = new MealAppApiFactory();
        using var client = factory.CreateClient();
        using var content = CreateSupportFormContent("Title", "Description", email: "guest@example.com");
        var image = new ByteArrayContent([1, 2, 3]);
        image.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(image, "image", fileName);
        using var response = await client.PostAsync("/api/support", content);
        await AssertSupportUnavailableAsync(response);
    }

    [Fact]
    public async Task SupportRequest_OversizedImageStillReturnsBadRequest()
    {
        await using var factory = new MealAppApiFactory();
        using var client = factory.CreateClient();
        using var content = CreateSupportFormContent("Title", "Description", email: "guest@example.com");
        var image = new ByteArrayContent(new byte[5 * 1024 * 1024 + 1]);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(image, "image", "test.png");
        using var response = await client.PostAsync("/api/support", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Image size cannot be greater than 5 MB.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SupportRequest_AuthenticatedAccountMustStillExist()
    {
        await using var factory = new MealAppApiFactory();
        using var client = factory.CreateClient();
        var email = CreateEmail();
        await RegisterUserAsync(factory, client, email);
        var token = await LoginAndGetTokenAsync(client, email);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Users.Remove(await context.Users.SingleAsync(user => user.Email == email));
            await context.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var content = CreateSupportFormContent("Title", "Description", email: "guest@example.com");
        using var response = await client.PostAsync("/api/support", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Authenticated user could not be found.", await response.Content.ReadAsStringAsync());
    }

    private static async Task RegisterUserAsync(MealAppApiFactory factory, HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            firstName = "Test",
            lastName = "User",
            email,
            password = "Pass123$"
        });

        await EnsureSuccessAsync(response);

        await ConfirmUserEmailAsync(factory, email);
    }

    private static MultipartFormDataContent CreateSupportFormContent(
        string title,
        string description,
        string? name = null,
        string? email = null,
        string? phone = null,
        string? imageContentType = null)
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent(title), "title" },
            { new StringContent(description), "description" }
        };

        if (name is not null)
        {
            content.Add(new StringContent(name), "name");
        }

        if (email is not null)
        {
            content.Add(new StringContent(email), "email");
        }

        if (phone is not null)
        {
            content.Add(new StringContent(phone), "phone");
        }

        if (imageContentType is not null)
        {
            var imageContent = new ByteArrayContent("test image"u8.ToArray());
            imageContent.Headers.ContentType = new MediaTypeHeaderValue(imageContentType);
            content.Add(imageContent, "image", "test.txt");
        }

        return content;
    }

    private static async Task ConfirmUserEmailAsync(MealAppApiFactory factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);

        Assert.NotNull(user);

        if (user.EmailConfirmed)
        {
            return;
        }

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var result = await userManager.ConfirmEmailAsync(user, token);

        Assert.True(result.Succeeded);
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
            FirstName = "Test",
            LastName = "Admin",
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

    private static async Task<int> CreateCategoryAsync(MealAppApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = new Category
        {
            Name = $"Delete Test Category {Guid.NewGuid():N}",
            Description = "Created by category delete test."
        };

        context.Categories.Add(category);
        await context.SaveChangesAsync();

        return category.Id;
    }

    private static async Task<CategoryDeleteSetup> CreateCategoryWithRecipesAsync(
        MealAppApiFactory factory,
        int recipeCount)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = new Category
        {
            Name = $"Used Category {Guid.NewGuid():N}",
            Description = "Created by category delete test."
        };

        for (var index = 0; index < recipeCount; index++)
        {
            category.Recipes.Add(new Recipe
            {
                Title = $"Recipe Using Category {Guid.NewGuid():N}",
                Description = "Created by category delete test.",
                PreparationTimeMinutes = 10,
                CookingTimeMinutes = 20,
                Servings = 4
            });
        }

        context.Categories.Add(category);
        await context.SaveChangesAsync();

        return new CategoryDeleteSetup(category.Id);
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

    private static async Task<ShoppingListSetup> CreateMealPlanShoppingListMergeSetupAsync(
        MealAppApiFactory factory,
        string email,
        bool includeManualItem = false,
        bool includeLegacyItem = false)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);

        Assert.NotNull(user);

        var category = new Category
        {
            Name = $"Test Recipe Category {Guid.NewGuid():N}"
        };
        var ingredientCategory = new IngredientCategory
        {
            Name = $"Test Ingredient Category {Guid.NewGuid():N}"
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
        var recipe = new Recipe
        {
            Title = $"Test Recipe {Guid.NewGuid():N}",
            Servings = 4,
            Category = category,
            RecipeIngredients =
            [
                new RecipeIngredient
                {
                    Ingredient = ingredient,
                    Unit = unit,
                    Quantity = 6
                }
            ]
        };
        var mealPlan = new MealPlan
        {
            UserId = user.Id,
            Recipe = recipe,
            Servings = 2,
            PlannedDate = DateTime.UtcNow.Date,
            CreatedAt = DateTime.UtcNow
        };
        var shoppingList = new ShoppingList
        {
            UserId = user.Id,
            Name = "Test Shopping List",
            CreatedAt = DateTime.UtcNow
        };

        if (includeManualItem || includeLegacyItem)
        {
            shoppingList.Items.Add(new ShoppingListItem
            {
                Ingredient = ingredient,
                Unit = unit,
                Quantity = 2,
                IsChecked = true,
                IsGenerated = includeLegacyItem ? null : false
            });
        }

        context.MealPlans.Add(mealPlan);
        context.ShoppingLists.Add(shoppingList);
        await context.SaveChangesAsync();

        return new ShoppingListSetup(
            shoppingList.Id,
            ingredient.Id,
            unit.Id,
            includeManualItem || includeLegacyItem ? shoppingList.Items.First().Id : 0,
            mealPlan.Id,
            recipe.Id);
    }

    private static async Task<ShoppingListSetup> CreateTwoRecipeMealPlanSetupAsync(
        MealAppApiFactory factory,
        string email)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);

        Assert.NotNull(user);

        var category = new Category
        {
            Name = $"Test Recipe Category {Guid.NewGuid():N}"
        };
        var ingredientCategory = new IngredientCategory
        {
            Name = $"Test Ingredient Category {Guid.NewGuid():N}"
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
        var firstRecipe = new Recipe
        {
            Title = $"First Test Recipe {Guid.NewGuid():N}",
            Servings = 4,
            Category = category,
            RecipeIngredients =
            [
                new RecipeIngredient
                {
                    Ingredient = ingredient,
                    Unit = unit,
                    Quantity = 6
                }
            ]
        };
        var secondRecipe = new Recipe
        {
            Title = $"Second Test Recipe {Guid.NewGuid():N}",
            Servings = 2,
            Category = category,
            RecipeIngredients =
            [
                new RecipeIngredient
                {
                    Ingredient = ingredient,
                    Unit = unit,
                    Quantity = 4
                }
            ]
        };
        var firstMealPlan = new MealPlan
        {
            UserId = user.Id,
            Recipe = firstRecipe,
            Servings = 2,
            PlannedDate = DateTime.UtcNow.Date,
            CreatedAt = DateTime.UtcNow
        };
        var secondMealPlan = new MealPlan
        {
            UserId = user.Id,
            Recipe = secondRecipe,
            Servings = 1,
            PlannedDate = DateTime.UtcNow.Date.AddDays(1),
            CreatedAt = DateTime.UtcNow
        };
        var shoppingList = new ShoppingList
        {
            UserId = user.Id,
            Name = "Test Shopping List",
            CreatedAt = DateTime.UtcNow
        };

        context.MealPlans.AddRange(firstMealPlan, secondMealPlan);
        context.ShoppingLists.Add(shoppingList);
        await context.SaveChangesAsync();

        return new ShoppingListSetup(
            shoppingList.Id,
            ingredient.Id,
            unit.Id,
            0,
            firstMealPlan.Id,
            firstRecipe.Id);
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

    private static async Task<int> AddCustomShoppingListItemAsync(
        MealAppApiFactory factory,
        int shoppingListId,
        string customName,
        decimal? quantity = null)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = new ShoppingListItem
        {
            ShoppingListId = shoppingListId,
            CustomName = customName,
            Quantity = quantity,
            IsChecked = false,
            IsGenerated = false
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

    private static async Task AssertInvalidLoginResponseAsync(HttpResponseMessage response)
    {
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = json.RootElement;

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(root.GetProperty("isSuccess").GetBoolean());
        Assert.Equal(401, root.GetProperty("statusCode").GetInt32());
        Assert.Equal(
            "Invalid email or password.",
            root.GetProperty("errorMessages")[0].GetString());
    }
}

public record ShoppingListSetup(
    int ShoppingListId,
    int IngredientId,
    int UnitId,
    int ItemId,
    int MealPlanId = 0,
    int RecipeId = 0);

public record CategoryDeleteSetup(int CategoryId);

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

            services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(
                    Path.Combine(AppContext.BaseDirectory, "data-protection-keys", _databaseName)));
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
