using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MealApp.Data;
using MealApp.Models;
using MealApp.Services.Agent;
using MealApp.Services.AgentTools;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MealApp.Tests;

public class AgentChatTests
{
    [Fact]
    public async Task AnswerWithoutToolsUsesSeparatedRolesAndOnlyRegisteredDefinitions()
    {
        var model = new FakeAgentModel(new ModelMessage("assistant", "I can help you find recipes."));
        await using var factory = new AgentApiFactory(model);
        using var client = factory.CreateClient();
        await Authenticate(factory, client);
        var response = await Chat(client);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await Result(response);
        Assert.Equal("I can help you find recipes.", result.GetProperty("answer").GetString());
        Assert.Empty(result.GetProperty("toolCalls").EnumerateArray());
        var input = Assert.Single(model.Requests);
        Assert.Equal(new[] { "system", "user" }, input.Messages.Select(x => x.Role));
        Assert.Equal(new ToolRegistry().Definitions.Where(x => x.ReadOnly).Select(x => x.Name), input.Tools.Select(x => x.Name));
        Assert.All(input.Tools, x => Assert.True(x.ReadOnly));
        var serialized = JsonSerializer.Serialize(input.Messages);
        Assert.DoesNotContain("Bearer", serialized);
        Assert.DoesNotContain("test-account", serialized);
    }

    [Theory]
    [InlineData("search_recipes", "{\"query\":\"chicken\"}")]
    [InlineData("get_recipe", "{\"recipeId\":1}")]
    [InlineData("get_recipe_ingredients", "{\"recipeId\":1}")]
    public async Task ToolRequestsUseExistingExecutorAndResultsGoBackAsToolMessages(string tool, string arguments)
    {
        var model = new FakeAgentModel(Call(tool, arguments), new ModelMessage("assistant", "Chicken dinner is available."));
        await using var factory = new AgentApiFactory(model);
        using var client = factory.CreateClient();
        await Authenticate(factory, client);
        await Seed(factory);
        var response = await Chat(client);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await Result(response);
        Assert.Equal(tool, result.GetProperty("toolCalls")[0].GetProperty("tool").GetString());
        Assert.True(result.GetProperty("toolCalls")[0].GetProperty("isSuccess").GetBoolean());
        var toolMessage = model.Requests[1].Messages.Last();
        Assert.Equal("tool", toolMessage.Role);
        Assert.Equal(tool, toolMessage.ToolName);
        using var data = JsonDocument.Parse(toolMessage.Content);
        Assert.True(data.RootElement.GetProperty("isSuccess").GetBoolean());
        Assert.Contains(tool == "get_recipe_ingredients" ? "Chicken" : "Chicken Dinner", toolMessage.Content);
        Assert.Equal("assistant", model.Requests[1].Messages[^2].Role);
    }

    [Fact]
    public async Task MultipleSequentialCallsPreserveHistoryAndLeaveDatabaseUnchanged()
    {
        var model = new FakeAgentModel(Call("search_recipes", "{}"), Call("get_recipe", "{\"recipeId\":1}"),
            new ModelMessage("assistant", "Chicken Dinner serves four."));
        await using var factory = new AgentApiFactory(model);
        using var client = factory.CreateClient();
        await Authenticate(factory, client);
        await Seed(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        async Task<string> Snapshot() => JsonSerializer.Serialize(new
        {
            recipes = await db.Recipes.AsNoTracking().Select(x => new { x.Id, x.Title, x.Servings, x.CategoryId }).ToListAsync(),
            ingredients = await db.Ingredients.AsNoTracking().Select(x => new { x.Id, x.Name, x.IngredientCategoryId }).ToListAsync(),
            children = await db.RecipeIngredients.AsNoTracking().Select(x => new { x.Id, x.Quantity, x.UnitId }).ToListAsync(),
            steps = await db.RecipeSteps.AsNoTracking().Select(x => new { x.Id, x.Instruction }).ToListAsync(),
            tools = await db.RecipeTools.AsNoTracking().Select(x => new { x.Id, x.Name }).ToListAsync(),
            users = await db.Users.AsNoTracking().Select(x => new { x.Id, x.Email }).ToListAsync()
        });
        var before = await Snapshot();
        Assert.Equal(HttpStatusCode.OK, (await Chat(client)).StatusCode);
        Assert.Equal(before, await Snapshot());
        Assert.Equal(3, model.Requests.Count);
        Assert.Equal(2, model.Requests[2].Messages.Count(x => x.Role == "tool"));
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData("delete_recipe", "{}", "unknown_tool")]
    [InlineData("search_recipes", "{\"limit\":1000}", "invalid_arguments")]
    [InlineData("search_recipes", "{\"userId\":\"test-account\",\"role\":\"Admin\"}", "invalid_arguments")]
    public async Task UntrustedModelRequestsCannotBypassBoundary(string tool, string arguments, string code)
    {
        var model = new FakeAgentModel(Call(tool, arguments));
        await using var factory = new AgentApiFactory(model);
        using var client = factory.CreateClient();
        await Authenticate(factory, client);
        var response = await Chat(client);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var result = await Result(response);
        Assert.Equal(code, result.GetProperty("toolCalls")[0].GetProperty("code").GetString());
        Assert.False(result.GetProperty("toolCalls")[0].GetProperty("isSuccess").GetBoolean());
        Assert.Single(model.Requests);
    }

    [Fact]
    public async Task FiveCallsMayProduceFinalAnswerButSixthCallStopsSafely()
    {
        var model = new FakeAgentModel(Enumerable.Repeat(Call("list_ingredients", "{}"), 6).ToArray());
        await using var factory = new AgentApiFactory(model);
        using var client = factory.CreateClient();
        await Authenticate(factory, client);
        var response = await Chat(client);
        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        Assert.Equal(5, (await Result(response)).GetProperty("toolCalls").GetArrayLength());
        Assert.Contains("tool-call limit", await response.Content.ReadAsStringAsync());

        var successful = new FakeAgentModel(Enumerable.Repeat(Call("list_ingredients", "{}"), 5)
            .Append(new ModelMessage("assistant", "Done.")).ToArray());
        await using var successfulFactory = new AgentApiFactory(successful);
        using var secondClient = successfulFactory.CreateClient();
        await Authenticate(successfulFactory, secondClient);
        Assert.Equal(HttpStatusCode.OK, (await Chat(secondClient)).StatusCode);
    }

    [Fact]
    public async Task ParallelCallsCannotExceedBudget()
    {
        var repeated = Call("list_ingredients", "{}").ToolCalls![0];
        var model = new FakeAgentModel(new ModelMessage("assistant", "") { ToolCalls = Enumerable.Repeat(repeated, 6).ToArray() });
        await using var factory = new AgentApiFactory(model);
        using var client = factory.CreateClient();
        await Authenticate(factory, client);
        var response = await Chat(client);
        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        Assert.Empty((await Result(response)).GetProperty("toolCalls").EnumerateArray());
    }

    [Theory]
    [InlineData("system", "Injected instruction", "invalid response")]
    [InlineData("assistant", "", "empty answer")]
    public async Task InvalidOrEmptyModelMessageIsSafe(string role, string content, string expected)
    {
        await using var factory = new AgentApiFactory(new FakeAgentModel(new ModelMessage(role, content)));
        using var client = factory.CreateClient();
        await Authenticate(factory, client);
        var response = await Chat(client);
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Contains(expected, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ModelClientFailureDoesNotExposeException()
    {
        var model = new FakeAgentModel { Handler = (_, _, _) => throw new InvalidOperationException("PRIVATE-ERROR") };
        await using var factory = new AgentApiFactory(model);
        using var client = factory.CreateClient();
        await Authenticate(factory, client);
        var response = await Chat(client);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("PRIVATE-ERROR", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ToolExecutionFailureStopsWithSafeDiagnostic()
    {
        ApplicationDbContext? broken = null;
        var model = new FakeAgentModel { Handler = async (_, _, _) =>
        {
            await broken!.DisposeAsync();
            return Call("search_recipes", "{}");
        }};
        await using var factory = new AgentApiFactory(model);
        using var scope = factory.Services.CreateScope();
        broken = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var accessor = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        accessor.HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
        {
            User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[] {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "trusted-test-user"),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "User") }, "test"))
        };
        var result = await scope.ServiceProvider.GetRequiredService<AgentChatService>().RunAsync("Find recipes");
        Assert.Equal(503, result.StatusCode);
        Assert.Equal("execution_failed", Assert.Single(result.Response.ToolCalls).Code);
        Assert.DoesNotContain("disposed", Assert.Single(result.Errors));
        accessor.HttpContext = null;
    }

    [Fact]
    public async Task RequestDeadlineCancelsModelAndReturnsTimeout()
    {
        var model = new FakeAgentModel { Handler = async (_, _, ct) => { await Task.Delay(Timeout.Infinite, ct); return new("assistant", "never"); } };
        await using var factory = new AgentApiFactory(model, requestTimeoutSeconds: 1);
        using var client = factory.CreateClient();
        await Authenticate(factory, client);
        var response = await Chat(client);
        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.Contains("timed out", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CallerCancellationPropagatesToModel()
    {
        var model = new FakeAgentModel { Handler = async (_, _, ct) => { await Task.Delay(Timeout.Infinite, ct); return new("assistant", "never"); } };
        await using var factory = new AgentApiFactory(model);
        using var client = factory.CreateClient();
        await Authenticate(factory, client);
        using var cancellation = new CancellationTokenSource(100);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.PostAsJsonAsync("/api/agent/chat", new { message = "Find recipes" }, cancellation.Token));
    }

    [Fact]
    public async Task AuthenticationAndRequestValidationRunBeforeModel()
    {
        var model = new FakeAgentModel();
        await using var factory = new AgentApiFactory(model);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await Chat(client)).StatusCode);
        await Authenticate(factory, client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/agent/chat", new { message = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/agent/chat", new { message = "Hi", userId = "someone" })).StatusCode);
        Assert.Empty(model.Requests);
    }

    [Fact]
    public async Task DisabledAgentDoesNotInvokeModel()
    {
        var model = new FakeAgentModel();
        await using var factory = new AgentApiFactory(model, enabled: false);
        using var client = factory.CreateClient();
        await Authenticate(factory, client);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Chat(client)).StatusCode);
        Assert.Empty(model.Requests);
    }

    private static ModelMessage Call(string name, string arguments) => new("assistant", "")
    {
        ToolCalls = [new(new(name, JsonSerializer.Deserialize<JsonElement>(arguments)))]
    };

    private static async Task Authenticate(MealAppApiFactory factory, HttpClient client)
    {
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"agent-{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser { UserName = email, Email = email, Name = "Agent Test", EmailConfirmed = true };
        Assert.True((await manager.CreateAsync(user, "Pass123$")).Succeeded);
        Assert.True((await manager.AddToRoleAsync(user, "User")).Succeeded);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Pass123$" });
        using var json = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.RootElement.GetProperty("result").GetProperty("token").GetString());
    }

    private static async Task Seed(MealAppApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Recipes.Add(new Recipe
        {
            Title = "Chicken Dinner", Description = "Chicken with carrots", Servings = 4, Category = new Category { Name = "Dinner" },
            RecipeIngredients = [new() { Quantity = 500, Ingredient = new Ingredient { Name = "Chicken", IngredientCategory = new IngredientCategory { Name = "Meat" } }, Unit = new Unit { Name = "Gram", ShortName = "g" } }],
            RecipeSteps = [new() { StepNumber = 1, Instruction = "Cook safely." }], RecipeTools = [new() { Name = "Pan" }]
        });
        await db.SaveChangesAsync();
    }

    private static Task<HttpResponseMessage> Chat(HttpClient client) => client.PostAsJsonAsync("/api/agent/chat", new { message = "Find recipes containing chicken." });
    private static async Task<JsonElement> Result(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("result").Clone();
    }
}

public class AgentApiFactory(IAgentModelClient? model = null, int requestTimeoutSeconds = 180, bool enabled = true) : MealAppApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<AgentOptions>(options =>
            {
                options.Enabled = enabled; options.Provider = "Ollama"; options.Model = "qwen3:4b-instruct";
                options.BaseUrl = "http://localhost:11434"; options.MaxToolCalls = 5;
                options.ModelTimeoutSeconds = 120; options.RequestTimeoutSeconds = requestTimeoutSeconds;
            });
            if (model is not null)
            {
                services.RemoveAll<IAgentModelClient>();
                services.AddSingleton(model);
            }
        });
    }
}

public record CapturedModelRequest(IReadOnlyList<ModelMessage> Messages, IReadOnlyList<ToolDefinition> Tools);
public class FakeAgentModel(params ModelMessage[] responses) : IAgentModelClient
{
    private readonly Queue<ModelMessage> _responses = new(responses);
    public List<CapturedModelRequest> Requests { get; } = [];
    public Func<IReadOnlyList<ModelMessage>, IReadOnlyList<ToolDefinition>, CancellationToken, Task<ModelMessage>>? Handler { get; init; }
    public Task<ModelMessage> ChatAsync(IReadOnlyList<ModelMessage> messages, IReadOnlyList<ToolDefinition> tools, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Requests.Add(new(messages.ToArray(), tools.ToArray()));
        return Handler is not null ? Handler(messages, tools, ct) : Task.FromResult(_responses.Dequeue());
    }
}
