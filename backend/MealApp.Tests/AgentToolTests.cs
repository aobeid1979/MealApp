using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using MealApp.Data;
using MealApp.DTOs.AgentTools;
using MealApp.Models;
using MealApp.Services;
using MealApp.Services.AgentTools;
using MealApp.Services.AgentActions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MealApp.Tests;

public class AgentToolTests
{
    [Fact]
    public void RegistryClassifiesFourReadToolsAndOneApprovalWriteTool()
    {
        var registry = new ToolRegistry();
        Assert.Equal(new[] { "get_recipe", "get_recipe_ingredients", "list_ingredients", "search_recipes" },
            registry.Definitions.Where(x => x.ReadOnly).Select(x => x.Name).OrderBy(x => x).ToArray());
        Assert.All(registry.Definitions.Where(x => x.ReadOnly), x =>
        {
            Assert.True(x.ReadOnly);
            Assert.False(x.InputSchema.GetProperty("additionalProperties").GetBoolean());
        });
        Assert.Equal(ToolCapability.Write, Assert.Single(registry.Definitions, x => !x.ReadOnly).Capability);
        Assert.False(registry.TryGet("delete_recipe", out _));
    }

    [Theory]
    [InlineData("chicken")]
    [InlineData("CHICKEN")]
    [InlineData("  ChIcKeN  ")]
    public async Task SearchIsCaseInsensitiveAndFiltersCategories(string text)
    {
        await using var factory = new MealAppApiFactory();
        using var client = factory.CreateClient();
        await Authenticate(factory, client);
        await Seed(factory);
        var data = await Success(client, "search_recipes", new { query = text, category = "DINNER", limit = 10 });
        Assert.Equal(2, data.GetArrayLength());
        Assert.Equal("Alpha Chicken", data[0].GetProperty("name").GetString());
        Assert.Equal("Beta Chicken", data[1].GetProperty("name").GetString());
    }

    [Fact]
    public async Task SearchBoundsResultsAndHasStableOrderIncludingDuplicateNames()
    {
        await using var factory = new MealAppApiFactory();
        using var client = factory.CreateClient();
        await Authenticate(factory, client);
        await Seed(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var category = await db.Categories.FirstAsync();
            for (var i = 0; i < 60; i++) db.Recipes.Add(new Recipe { Title = "Same Name", CategoryId = category.Id, Servings = 1 });
            await db.SaveChangesAsync();
        }
        var first = await Success(client, "search_recipes", new { limit = 50 });
        var second = await Success(client, "search_recipes", new { limit = 50 });
        Assert.Equal(50, first.GetArrayLength());
        Assert.Equal(first.GetRawText(), second.GetRawText());
        Assert.Equal(10, (await Success(client, "search_recipes", new { })).GetArrayLength());
        Assert.Single((await Success(client, "search_recipes", new { limit = 1 })).EnumerateArray());
    }

    [Fact]
    public async Task GetRecipeAndIngredientsReturnDtoDataWithOrderedChildren()
    {
        await using var factory = new MealAppApiFactory();
        using var client = factory.CreateClient();
        await Authenticate(factory, client, "Admin");
        var id = await Seed(factory);
        var data = await Success(client, "get_recipe", new { recipeId = id });
        Assert.Equal("Beta Chicken", data.GetProperty("name").GetString());
        Assert.Equal(4, data.GetProperty("servings").GetInt32());
        Assert.Equal("Dinner", data.GetProperty("category").GetString());
        Assert.Equal(1, data.GetProperty("steps")[0].GetProperty("stepNumber").GetInt32());
        Assert.Equal("Bowl", data.GetProperty("tools")[0].GetString());
        Assert.False(data.TryGetProperty("mealPlans", out _));
        var ingredients = await Success(client, "get_recipe_ingredients", new { recipeId = id });
        Assert.Equal(2, ingredients.GetArrayLength());
        Assert.Equal("Carrot", ingredients[0].GetProperty("ingredientName").GetString());
        Assert.Equal(200m, ingredients[0].GetProperty("quantity").GetDecimal());
        Assert.Equal("g", ingredients[0].GetProperty("unitShortName").GetString());
        // The public controller keeps its existing contract while sharing the query.
        var detailsResponse = await client.GetAsync($"/api/recipe/{id}/details");
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        using var details = JsonDocument.Parse(await detailsResponse.Content.ReadAsStringAsync());
        Assert.Equal("Beta Chicken", details.RootElement.GetProperty("result").GetProperty("title").GetString());
    }

    [Theory]
    [InlineData("get_recipe")]
    [InlineData("get_recipe_ingredients")]
    public async Task MissingRecipeReturnsStructuredNotFound(string tool)
    {
        await using var factory = new MealAppApiFactory();
        using var client = factory.CreateClient();
        await Authenticate(factory, client);
        var response = await client.PostAsJsonAsync("/api/agent-tools/execute", new { tool, arguments = new { recipeId = int.MaxValue } });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(body.RootElement.GetProperty("isSuccess").GetBoolean());
        Assert.Equal("not_found", body.RootElement.GetProperty("result").GetProperty("code").GetString());
        Assert.Equal("Recipe not found.", body.RootElement.GetProperty("errorMessages")[0].GetString());
    }

    [Fact]
    public async Task IngredientSearchIsCaseInsensitiveCategorizedAndBounded()
    {
        await using var factory = new MealAppApiFactory();
        using var client = factory.CreateClient();
        await Authenticate(factory, client);
        await Seed(factory);
        var data = await Success(client, "list_ingredients", new { query = "CAR", category = "VEGETABLES", limit = 1 });
        Assert.Single(data.EnumerateArray());
        Assert.Equal("Carrot", data[0].GetProperty("name").GetString());
        Assert.Equal("Vegetables", data[0].GetProperty("ingredientCategory").GetString());
        Assert.Empty((await Success(client, "list_ingredients", new { query = "Chicken", category = "Vegetables" })).EnumerateArray());
    }

    [Theory]
    [InlineData("delete_recipe", "{}", "unknown_tool")]
    [InlineData("System.IO.File.ReadAllText", "{}", "unknown_tool")]
    [InlineData("search_recipes", "{\"limit\":51}", "invalid_arguments")]
    [InlineData("search_recipes", "{\"limit\":0}", "invalid_arguments")]
    [InlineData("search_recipes", "{\"limit\":1.5}", "invalid_arguments")]
    [InlineData("search_recipes", "{\"query\":123}", "invalid_arguments")]
    [InlineData("search_recipes", "{\"userId\":\"other-user\",\"role\":\"Admin\"}", "invalid_arguments")]
    [InlineData("search_recipes", "{\"sql\":\"DELETE FROM Recipes\"}", "invalid_arguments")]
    [InlineData("search_recipes", "{\"url\":\"https://example.com\"}", "invalid_arguments")]
    [InlineData("search_recipes", "{\"limit\":1,\"limit\":2}", "invalid_arguments")]
    [InlineData("get_recipe", "{}", "invalid_arguments")]
    [InlineData("get_recipe", "{\"recipeId\":-1}", "invalid_arguments")]
    [InlineData("list_ingredients", "null", "invalid_arguments")]
    public async Task InvalidRequestsAreRejected(string tool, string arguments, string code)
    {
        await using var factory = new MealAppApiFactory();
        using var client = factory.CreateClient();
        await Authenticate(factory, client);
        var response = await client.PostAsync("/api/agent-tools/execute", new StringContent(
            "{\"tool\":\"" + tool + "\",\"arguments\":" + arguments + "}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, body.RootElement.GetProperty("result").GetProperty("code").GetString());
    }

    [Fact]
    public async Task AuthenticationIsRequiredAndMalformedJsonIsRejected()
    {
        await using var factory = new MealAppApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/agent-tools/execute", new { tool = "search_recipes", arguments = new { } })).StatusCode);
        await Authenticate(factory, client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/agent-tools/execute", new StringContent("{bad", Encoding.UTF8, "application/json"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/agent-tools/execute", new { tool = "search_recipes", arguments = new { }, role = "Admin" })).StatusCode);
    }

    [Fact]
    public async Task EveryToolLeavesDatabaseAndTrackingUnchanged()
    {
        await using var factory = new MealAppApiFactory();
        using var client = factory.CreateClient();
        await Authenticate(factory, client);
        var id = await Seed(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        async Task<string> Snapshot() => JsonSerializer.Serialize(new {
            recipes = await db.Recipes.AsNoTracking().Select(x => new { x.Id, x.Title, x.Servings, x.CategoryId }).ToListAsync(),
            ingredients = await db.Ingredients.AsNoTracking().Select(x => new { x.Id, x.Name, x.IngredientCategoryId }).ToListAsync(),
            quantities = await db.RecipeIngredients.AsNoTracking().Select(x => new { x.Id, x.Quantity, x.UnitId, x.IngredientId }).ToListAsync(),
            steps = await db.RecipeSteps.AsNoTracking().Select(x => new { x.Id, x.StepNumber, x.Instruction }).ToListAsync(),
            tools = await db.RecipeTools.AsNoTracking().Select(x => new { x.Id, x.Name }).ToListAsync()
        });
        var before = await Snapshot();
        await Success(client, "search_recipes", new { });
        await Success(client, "list_ingredients", new { });
        await Success(client, "get_recipe", new { recipeId = id });
        await Success(client, "get_recipe_ingredients", new { recipeId = id });
        Assert.Equal(before, await Snapshot());
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task ExecutorUsesServerIdentityAndAuditsSuccessAndValidationFailure()
    {
        await using var factory = new MealAppApiFactory();
        await Seed(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = new CapturingLogger();
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "trusted-user"), new Claim(ClaimTypes.Role, "User") }, "test"))
        }};
        var executor = new AgentToolExecutor(new ToolRegistry(), new RecipeReadService(db), db, accessor, logger,
            new AgentToolPolicy(), scope.ServiceProvider.GetRequiredService<AgentActionService>());
        Assert.Equal(200, (await executor.ExecuteAsync(Request("search_recipes", "{}"))).StatusCode);
        Assert.Equal(400, (await executor.ExecuteAsync(Request("search_recipes", "{\"userId\":\"spoofed-user\"}"))).StatusCode);
        Assert.Empty(db.ChangeTracker.Entries());
        var executionAudits = logger.Messages.Where(x => x.StartsWith("Agent tool audit:")).ToArray();
        Assert.Equal(2, executionAudits.Length);
        Assert.All(executionAudits, x => { Assert.Contains("trusted-user", x); Assert.Contains("Timestamp=", x); Assert.Contains("DurationMs=", x); Assert.DoesNotContain("spoofed-user", x); });
        Assert.Contains(logger.Messages, x => x.Contains("ValidationFailure=True"));
        accessor.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        Assert.Equal(401, (await executor.ExecuteAsync(Request("search_recipes", "{}"))).StatusCode);
    }

    private static ToolExecutionRequest Request(string tool, string json)
        => new() { Tool = tool, Arguments = JsonSerializer.Deserialize<JsonElement>(json) };

    [Fact]
    public async Task UnexpectedExecutionFailureIsSafeAndAudited()
    {
        await using var factory = new MealAppApiFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = new CapturingLogger();
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "trusted-user"), new Claim(ClaimTypes.Role, "User") }, "test"))
        }};
        var executor = new AgentToolExecutor(new ToolRegistry(), new RecipeReadService(db), db, accessor, logger,
            new AgentToolPolicy(), scope.ServiceProvider.GetRequiredService<AgentActionService>());
        await db.DisposeAsync();
        var result = await executor.ExecuteAsync(Request("search_recipes", "{}"));
        Assert.Equal(500, result.StatusCode);
        Assert.Equal("execution_failed", result.Code);
        Assert.Equal("Unable to execute the tool. Please try again.", Assert.Single(result.Errors));
        Assert.Null(result.Data);
        Assert.Contains(logger.Messages, x => x.Contains("Code=execution_failed"));
    }

    private static async Task<JsonElement> Success(HttpClient client, string tool, object arguments)
    {
        var response = await client.PostAsJsonAsync("/api/agent-tools/execute", new { tool, arguments });
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        Assert.True(document.RootElement.GetProperty("isSuccess").GetBoolean(), body);
        return document.RootElement.GetProperty("result").GetProperty("data").Clone();
    }

    private static async Task Authenticate(MealAppApiFactory factory, HttpClient client, string role = "User")
    {
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"tool-{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser { UserName = email, Email = email, Name = "Tool Test", EmailConfirmed = true };
        Assert.True((await manager.CreateAsync(user, "Pass123$")).Succeeded);
        Assert.True((await manager.AddToRoleAsync(user, role)).Succeeded);
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Pass123$" });
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.RootElement.GetProperty("result").GetProperty("token").GetString());
    }

    private static async Task<int> Seed(MealAppApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = new Category { Name = "Dinner" };
        var ingredientCategory = new IngredientCategory { Name = "Vegetables" };
        var carrot = new Ingredient { Name = "Carrot", IngredientCategory = ingredientCategory };
        var chicken = new Ingredient { Name = "Chicken", IngredientCategory = new IngredientCategory { Name = "Meat" } };
        var unit = new Unit { Name = "Gram", ShortName = "g" };
        var recipe = new Recipe { Title = "Beta Chicken", Description = "Roasted vegetables", Category = category, Servings = 4,
            RecipeIngredients = [new() { Ingredient = chicken, Unit = unit, Quantity = 500 }, new() { Ingredient = carrot, Unit = unit, Quantity = 200 }],
            RecipeSteps = [new() { StepNumber = 2, Instruction = "Serve" }, new() { StepNumber = 1, Instruction = "Cook" }],
            RecipeTools = [new() { Name = "Pan" }, new() { Name = "Bowl" }]
        };
        db.Recipes.AddRange(recipe, new Recipe { Title = "Alpha Chicken", Category = category, Servings = 1 },
            new Recipe { Title = "Chicken Salad", Category = new Category { Name = "Lunch" }, Servings = 1 });
        await db.SaveChangesAsync();
        return recipe.Id;
    }

    private sealed class CapturingLogger : ILogger<AgentToolExecutor>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }
}
