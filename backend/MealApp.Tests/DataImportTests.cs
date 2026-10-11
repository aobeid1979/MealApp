using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MealApp.Data;
using MealApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MealApp.Tests;

public class DataImportTests
{
    [Theory]
    [InlineData("ingredients")]
    [InlineData("ingredients/preview")]
    [InlineData("recipes")]
    [InlineData("recipes/preview")]
    public async Task AllEndpointsRequireAdmin(string path)
    {
        await using var factory = new MealAppApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync($"/api/import/{path}", new { items = new object[0] })).StatusCode);
        await Authenticate(factory, client, "User");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/import/{path}", new { items = new object[0] })).StatusCode);
    }

    [Fact]
    public async Task IngredientImportNormalizesAndSkipsDuplicatesWithoutChangingExistingData()
    {
        await using var factory = new MealAppApiFactory();
        using var client = factory.CreateClient();
        await Authenticate(factory, client);
        await Seed(factory);
        var response = await client.PostAsJsonAsync("/api/import/ingredients", new { items = new[] {
            new { name = "  Green   Peas ", ingredientCategory = " VEGETABLES ", description = "Fresh peas" },
            new { name = "green peas", ingredientCategory = "Vegetables", description = "Changed" },
            new { name = " TOMATO ", ingredientCategory = "Vegetables", description = "Changed" }
        }});
        var result = await Result(response);
        Assert.Equal(1, result.GetProperty("imported").GetInt32());
        Assert.Equal(2, result.GetProperty("duplicates").GetInt32());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(3, await db.Ingredients.CountAsync());
        Assert.Equal("Fresh peas", (await db.Ingredients.SingleAsync(x => x.Name == "Green Peas")).Description);
        Assert.Null((await db.Ingredients.SingleAsync(x => x.Name == "Tomato")).Description);
    }

    [Theory]
    [InlineData("", "Vegetables", "name")]
    [InlineData("Peas", "Unknown", "ingredientCategory")]
    public async Task InvalidIngredientIsReportedAndNotWritten(string name, string category, string field)
    {
        await using var factory = new MealAppApiFactory();
        using var client = factory.CreateClient();
        await Authenticate(factory, client);
        await Seed(factory);
        var result = await Result(await client.PostAsJsonAsync("/api/import/ingredients", new { items = new[] { new { name, ingredientCategory = category } } }));
        Assert.Equal(0, result.GetProperty("imported").GetInt32());
        Assert.Contains(result.GetProperty("validationErrors").EnumerateArray(), x => x.GetProperty("field").GetString() == field);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Ingredients.CountAsync());
    }

    [Fact]
    public async Task PreviewDoesNotWriteAndExecutionRevalidates()
    {
        await using var factory = new MealAppApiFactory();
        using var client = factory.CreateClient();
        await Authenticate(factory, client);
        await Seed(factory);
        var ingredient = new { items = new[] { new { name = "Peas", ingredientCategory = "Vegetables" } } };
        Assert.Equal(1, (await Result(await client.PostAsJsonAsync("/api/import/ingredients/preview", ingredient))).GetProperty("ready").GetInt32());
        Assert.Equal(1, (await Result(await client.PostAsJsonAsync("/api/import/recipes/preview", Recipe()))).GetProperty("ready").GetInt32());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(2, await db.Ingredients.CountAsync());
        Assert.Empty(await db.Recipes.ToListAsync());
        db.Units.RemoveRange(db.Units);
        await db.SaveChangesAsync();
        Assert.Equal(0, (await Result(await client.PostAsJsonAsync("/api/import/recipes", Recipe()))).GetProperty("imported").GetInt32());
    }

    [Fact]
    public async Task CompleteRecipeReusesIngredientsAndRepeatedImportIsIdempotent()
    {
        await using var factory = new MealAppApiFactory();
        using var client = factory.CreateClient();
        await Authenticate(factory, client);
        await Seed(factory);
        Assert.Equal(1, (await Result(await client.PostAsJsonAsync("/api/import/recipes", Recipe()))).GetProperty("imported").GetInt32());
        Assert.Equal(1, (await Result(await client.PostAsJsonAsync("/api/import/recipes", Recipe()))).GetProperty("duplicates").GetInt32());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(2, await db.Ingredients.CountAsync());
        var recipe = await db.Recipes.Include(x => x.RecipeIngredients).Include(x => x.RecipeSteps).Include(x => x.RecipeTools).SingleAsync();
        Assert.Equal(2, recipe.RecipeIngredients.Count);
        Assert.Equal(2, recipe.RecipeSteps.Count);
        Assert.Single(recipe.RecipeTools);
        Assert.Equal(300m, recipe.RecipeIngredients.Sum(x => x.Quantity));
    }

    [Theory]
    [InlineData("Unknown", "Tomato", "g")]
    [InlineData("Salad", "Unknown", "g")]
    [InlineData("Salad", "Tomato", "unknown")]
    public async Task FailedRecipeHasNoPartialData(string category, string ingredient, string unit)
    {
        await using var factory = new MealAppApiFactory();
        using var client = factory.CreateClient();
        await Authenticate(factory, client);
        await Seed(factory);
        var result = await Result(await client.PostAsJsonAsync("/api/import/recipes", Recipe(category, ingredient, unit)));
        Assert.Equal(1, result.GetProperty("skipped").GetInt32());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(0, await db.Recipes.CountAsync());
        Assert.Equal(0, await db.RecipeIngredients.CountAsync());
        Assert.Equal(0, await db.RecipeSteps.CountAsync());
        Assert.Equal(0, await db.RecipeTools.CountAsync());
        Assert.Equal(2, await db.Ingredients.CountAsync());
    }

    [Theory]
    [InlineData("{invalid")]
    [InlineData("{\"items\":null}")]
    [InlineData("{\"items\":[]}")]
    [InlineData("{\"items\":[{\"servings\":\"wrong\"}]}")]
    public async Task MalformedOrEmptyInputReturnsStandardBadRequest(string json)
    {
        await using var factory = new MealAppApiFactory();
        using var client = factory.CreateClient();
        await Authenticate(factory, client);
        var response = await client.PostAsync("/api/import/recipes", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(body.RootElement.GetProperty("isSuccess").GetBoolean());
    }

    private static object Recipe(string category = "Salad", string ingredient = "Tomato", string unit = "g") => new
    {
        items = new[] { new {
            name = "  Simple   Salad ", category, servings = 2, preparationTimeMinutes = 10,
            ingredients = new[] { new { ingredient, quantity = 200, unit }, new { ingredient = "Cucumber", quantity = 100, unit = "Gram" } },
            steps = new[] { new { stepNumber = 2, instruction = "Serve." }, new { stepNumber = 1, instruction = "Chop and mix." } },
            tools = new[] { "Knife" }
        }}
    };

    private static async Task Seed(MealAppApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = new IngredientCategory { Name = "Vegetables" };
        db.Categories.Add(new Category { Name = "Salad" });
        db.Ingredients.AddRange(new Ingredient { Name = "Tomato", IngredientCategory = category }, new Ingredient { Name = "Cucumber", IngredientCategory = category });
        db.Units.Add(new Unit { Name = "Gram", ShortName = "g" });
        await db.SaveChangesAsync();
    }

    private static async Task Authenticate(MealAppApiFactory factory, HttpClient client, string role = "Admin")
    {
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"import-{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser { UserName = email, Email = email, Name = "Import Test", EmailConfirmed = true };
        Assert.True((await manager.CreateAsync(user, "Pass123$")).Succeeded);
        Assert.True((await manager.AddToRoleAsync(user, role)).Succeeded);
        var result = await Result(await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Pass123$" }));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.GetProperty("token").GetString());
    }

    private static async Task<JsonElement> Result(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        using var document = JsonDocument.Parse(body);
        Assert.True(document.RootElement.GetProperty("isSuccess").GetBoolean());
        return document.RootElement.GetProperty("result").Clone();
    }
}
