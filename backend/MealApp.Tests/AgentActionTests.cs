using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using MealApp.Data;
using MealApp.Models;
using MealApp.Services.AgentActions;
using MealApp.Services.AgentTools;
using MealApp.Services.Agent;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using System.Collections.Concurrent;

namespace MealApp.Tests;

public class AgentActionTests
{
    [Theory]
    [InlineData("search_recipes", "User", ToolPolicyDecision.Allow)]
    [InlineData("get_recipe", "Admin", ToolPolicyDecision.Allow)]
    [InlineData("create_recipe_draft", "Admin", ToolPolicyDecision.RequireApproval)]
    [InlineData("create_recipe_draft", "User", ToolPolicyDecision.Deny)]
    public void PolicyUsesTrustedCapabilityAndAuthenticatedRole(string name, string role, ToolPolicyDecision expected)
    {
        Assert.True(new ToolRegistry().TryGet(name, out var definition));
        Assert.Equal(expected, new AgentToolPolicy().Evaluate(definition, Principal(role)));
        Assert.Equal(ToolPolicyDecision.Deny, new AgentToolPolicy().Evaluate(definition, new ClaimsPrincipal()));
    }

    [Fact]
    public void UnregisteredWriteAndApprovalToolsAreNotAvailable()
    {
        var registry = new ToolRegistry();
        Assert.False(registry.TryGet("approve_action", out _));
        Assert.False(registry.TryGet("execute_action", out _));
        Assert.Equal(ToolPolicyDecision.Deny, new AgentToolPolicy().Evaluate(
            new("another_write", "", registry.Definitions.First().InputSchema, ToolCapability.Write), Principal("Admin")));
    }

    [Fact]
    public async Task ValidProposalNormalizesPayloadButCreatesNoRecipe()
    {
        await using var factory = new AgentApiFactory(new FakeAgentModel());
        using var client = factory.CreateClient();
        var userId = await Authenticate(factory, client, "Admin");
        await Seed(factory);
        var id = await Propose(client);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(0, await db.Recipes.CountAsync());
        Assert.Equal(0, await db.RecipeIngredients.CountAsync());
        var pending = scope.ServiceProvider.GetRequiredService<IPendingActionStore>().Find(id)!;
        Assert.Equal(userId, pending.RequestedByUserId);
        Assert.Equal(PendingActionStatus.Pending, pending.Status);
        var list = await Read(await client.GetAsync("/api/agent-actions/pending"));
        var proposal = Assert.Single(list.GetProperty("result").EnumerateArray());
        Assert.Equal("RequireApproval", proposal.GetProperty("policyDecision").GetString());
        Assert.Equal("Simple Chicken Pasta", proposal.GetProperty("payload").GetProperty("name").GetString());
        Assert.Equal("A simple dinner.", proposal.GetProperty("payload").GetProperty("description").GetString());
        Assert.Equal(1, proposal.GetProperty("payload").GetProperty("steps")[0].GetProperty("stepNumber").GetInt32());
        Assert.Equal("Gram", proposal.GetProperty("payload").GetProperty("ingredients")[0].GetProperty("unit").GetString());
    }

    [Theory]
    [InlineData("ingredient")]
    [InlineData("category")]
    [InlineData("unit")]
    [InlineData("quantity")]
    [InlineData("precision")]
    [InlineData("name")]
    [InlineData("missingName")]
    [InlineData("nameLength")]
    [InlineData("descriptionLength")]
    [InlineData("servings")]
    [InlineData("time")]
    [InlineData("duplicateStep")]
    [InlineData("duplicateIngredient")]
    [InlineData("duplicateTool")]
    [InlineData("blankInstruction")]
    [InlineData("tooManyIngredients")]
    [InlineData("tooManySteps")]
    [InlineData("tooManyTools")]
    [InlineData("identity")]
    [InlineData("approval")]
    [InlineData("nestedIdentity")]
    public async Task InvalidOrSpoofedProposalsNeverBecomePending(string invalid)
    {
        await using var factory = new AgentApiFactory(new FakeAgentModel());
        using var client = factory.CreateClient();
        await Authenticate(factory, client, "Admin");
        await Seed(factory);
        var payload = Payload();
        switch (invalid)
        {
            case "ingredient": payload["ingredients"]![0]!["ingredient"] = "Unknown"; break;
            case "category": payload["category"] = "Unknown"; break;
            case "unit": payload["ingredients"]![0]!["unit"] = "Unknown"; break;
            case "quantity": payload["ingredients"]![0]!["quantity"] = 0; break;
            case "precision": payload["ingredients"]![0]!["quantity"] = 1.001m; break;
            case "name": payload["name"] = "   "; break;
            case "missingName": payload.Remove("name"); break;
            case "nameLength": payload["name"] = new string('a', 201); break;
            case "descriptionLength": payload["description"] = new string('a', 5001); break;
            case "servings": payload["servings"] = 51; break;
            case "time": payload["preparationTimeMinutes"] = -1; break;
            case "duplicateStep": payload["steps"]![0]!["stepNumber"] = 1; break;
            case "duplicateIngredient": payload["ingredients"]!.AsArray().Add(payload["ingredients"]![0]!.DeepClone()); break;
            case "duplicateTool": payload["tools"]!.AsArray().Add("knife"); break;
            case "blankInstruction": payload["steps"]![0]!["instruction"] = " "; break;
            case "tooManyIngredients": payload["ingredients"] = Repeat(payload["ingredients"]![0]!, 101); break;
            case "tooManySteps": payload["steps"] = Repeat(payload["steps"]![0]!, 101); break;
            case "tooManyTools": payload["tools"] = Repeat(JsonValue.Create("Pan")!, 101); break;
            case "identity": payload["userId"] = "someone"; break;
            case "approval": payload["status"] = "Approved"; payload["approved"] = true; break;
            case "nestedIdentity": payload["ingredients"]![0]!["ingredientId"] = 1; break;
        }
        var response = await client.PostAsJsonAsync("/api/agent-tools/execute", new { tool = "create_recipe_draft", arguments = payload });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Empty(scope.ServiceProvider.GetRequiredService<IPendingActionStore>().ListPending());
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Recipes.CountAsync());
    }

    [Fact]
    public async Task UserCannotProposeListApproveOrReject()
    {
        await using var factory = new AgentApiFactory(new FakeAgentModel());
        using var admin = factory.CreateClient();
        await Authenticate(factory, admin, "Admin");
        await Seed(factory);
        var id = await Propose(admin);
        using var user = factory.CreateClient();
        await Authenticate(factory, user, "User");
        Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsJsonAsync("/api/agent-tools/execute", new { tool = "create_recipe_draft", arguments = Payload() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/agent-actions/pending")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsync($"/api/agent-actions/{id}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsync($"/api/agent-actions/{id}/reject", null)).StatusCode);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync($"/api/agent-actions/{id}/approve", null)).StatusCode);
    }

    [Fact]
    public async Task RejectedActionCannotExecuteAndDisappearsFromPending()
    {
        await using var factory = new AgentApiFactory(new FakeAgentModel());
        using var client = factory.CreateClient();
        await Authenticate(factory, client, "Admin");
        await Seed(factory);
        var id = await Propose(client);
        var rejected = await client.PostAsync($"/api/agent-actions/{id}/reject", null);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Equal("Rejected", (await Read(rejected)).GetProperty("result").GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/agent-actions/{id}/approve", null)).StatusCode);
        Assert.Empty((await Read(await client.GetAsync("/api/agent-actions/pending"))).GetProperty("result").EnumerateArray());
        using var scope = factory.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Recipes.CountAsync());
    }

    [Fact]
    public async Task ApprovalExecutesExactNormalizedPayloadOnceAndCannotBeOverridden()
    {
        await using var factory = new AgentApiFactory(new FakeAgentModel());
        using var client = factory.CreateClient();
        await Authenticate(factory, client, "Admin");
        await Seed(factory);
        var id = await Propose(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/agent-actions/{id}/approve", new { name = "Altered Recipe", approved = true })).StatusCode);
        var approved = await client.PostAsync($"/api/agent-actions/{id}/approve", null);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var result = (await Read(approved)).GetProperty("result");
        Assert.Equal("Executed", result.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/agent-actions/{id}/approve", null)).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var recipe = await db.Recipes.Include(x => x.RecipeIngredients).Include(x => x.RecipeSteps).Include(x => x.RecipeTools).SingleAsync();
        Assert.Equal("Simple Chicken Pasta", recipe.Title);
        Assert.Equal("A simple dinner.", recipe.Description);
        Assert.Equal(4, recipe.Servings);
        Assert.Equal(15, recipe.PreparationTimeMinutes);
        Assert.Equal(25, recipe.CookingTimeMinutes);
        Assert.Equal(500m, Assert.Single(recipe.RecipeIngredients).Quantity);
        Assert.Equal(new[] { 1, 2 }, recipe.RecipeSteps.Select(x => x.StepNumber).OrderBy(x => x));
        Assert.Equal(new[] { "Knife", "Pan" }, recipe.RecipeTools.Select(x => x.Name).OrderBy(x => x));
        Assert.Equal(1, await db.Ingredients.CountAsync());
        Assert.Equal(1, await db.Units.CountAsync());
    }

    [Fact]
    public async Task ConcurrentApprovalsCreateOnlyOneRecipe()
    {
        await using var factory = new AgentApiFactory(new FakeAgentModel());
        using var client = factory.CreateClient();
        await Authenticate(factory, client, "Admin");
        await Seed(factory);
        var id = await Propose(client);
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.PostAsync($"/api/agent-actions/{id}/approve", null)));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Equal(7, responses.Count(x => x.StatusCode == HttpStatusCode.Conflict));
        using var scope = factory.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Recipes.CountAsync());
    }

    [Fact]
    public async Task ReferencesCannotBeReboundToReplacementRecordsAfterProposal()
    {
        await using var factory = new AgentApiFactory(new FakeAgentModel());
        using var client = factory.CreateClient();
        await Authenticate(factory, client, "Admin");
        await Seed(factory);
        var id = await Propose(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var original = await db.Ingredients.SingleAsync();
            db.Ingredients.Remove(original);
            db.Ingredients.Add(new Ingredient { Name = original.Name, IngredientCategoryId = original.IngredientCategoryId });
            await db.SaveChangesAsync();
        }
        var response = await client.PostAsync($"/api/agent-actions/{id}/approve", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("references have changed", await response.Content.ReadAsStringAsync());
        using var verification = factory.Services.CreateScope();
        Assert.Equal(PendingActionStatus.Failed, verification.ServiceProvider.GetRequiredService<IPendingActionStore>().Find(id)!.Status);
        Assert.Equal(0, await verification.ServiceProvider.GetRequiredService<ApplicationDbContext>().Recipes.CountAsync());
    }

    [Fact]
    public async Task DuplicateCreatedAfterProposalFailsRevalidationAndCannotReplay()
    {
        await using var factory = new AgentApiFactory(new FakeAgentModel());
        using var client = factory.CreateClient();
        await Authenticate(factory, client, "Admin");
        await Seed(factory);
        var id = await Propose(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Recipes.Add(new Recipe { Title = "Simple Chicken Pasta", CategoryId = (await db.Categories.SingleAsync()).Id, Servings = 1 });
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/agent-actions/{id}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/agent-actions/{id}/approve", null)).StatusCode);
        using var verification = factory.Services.CreateScope();
        Assert.Equal(1, await verification.ServiceProvider.GetRequiredService<ApplicationDbContext>().Recipes.CountAsync());
    }

    [Fact]
    public async Task RevokedRequesterRoleInvalidatesApproval()
    {
        await using var factory = new AgentApiFactory(new FakeAgentModel());
        using var requester = factory.CreateClient();
        var userId = await Authenticate(factory, requester, "Admin");
        await Seed(factory);
        var id = await Propose(requester);
        using var reviewer = factory.CreateClient();
        await Authenticate(factory, reviewer, "Admin");
        using (var scope = factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await manager.RemoveFromRoleAsync((await manager.FindByIdAsync(userId))!, "Admin")).Succeeded);
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await reviewer.PostAsync($"/api/agent-actions/{id}/approve", null)).StatusCode);
    }

    [Fact]
    public async Task ChatReturnsApprovalRequiredAndModelCannotApproveOrClaimPublication()
    {
        var model = new FakeAgentModel(new ModelMessage("assistant", "")
        { ToolCalls = [new(new("create_recipe_draft", JsonSerializer.SerializeToElement(Payload())))] },
            new ModelMessage("assistant", "Recipe published!")
            { ToolCalls = [new(new("approve_action", JsonSerializer.SerializeToElement(new { approved = true })))] });
        await using var factory = new AgentApiFactory(model);
        using var client = factory.CreateClient();
        await Authenticate(factory, client, "Admin");
        await Seed(factory);
        var response = await client.PostAsJsonAsync("/api/agent/chat", new { message = "Create a recipe draft" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await Read(response)).GetProperty("result");
        Assert.Contains("awaiting human Admin approval", result.GetProperty("answer").GetString());
        Assert.Equal("approval_required", result.GetProperty("toolCalls")[0].GetProperty("code").GetString());
        Assert.True(result.GetProperty("toolCalls")[0].GetProperty("isSuccess").GetBoolean());
        Assert.Equal("Pending", result.GetProperty("pendingAction").GetProperty("status").GetString());
        Assert.Contains(model.Requests[0].Tools, x => x.Name == "create_recipe_draft" && !x.ReadOnly);
        Assert.Empty(model.Requests[1].Tools);
        Assert.Contains("approval_required", model.Requests[1].Messages.Last().Content);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Recipes.CountAsync());
        Assert.Single(scope.ServiceProvider.GetRequiredService<IPendingActionStore>().ListPending());
    }

    [Fact]
    public async Task MissingActionIsNotFoundAndNewStoreDoesNotRetainPendingState()
    {
        await using var factory = new AgentApiFactory(new FakeAgentModel());
        using var client = factory.CreateClient();
        await Authenticate(factory, client, "Admin");
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/agent-actions/{Guid.NewGuid()}/approve", null)).StatusCode);
        Assert.Empty(new InMemoryPendingActionStore().ListPending());
    }

    [Fact]
    public async Task AuditRecordsWriteLifecycleWithoutPayloadOrCredentials()
    {
        var audit = new AuditProvider();
        await using var factory = new AuditedFactory(audit);
        using var client = factory.CreateClient();
        var userId = await Authenticate(factory, client, "Admin");
        await Seed(factory);
        var id = await Propose(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/agent-actions/{id}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/agent-actions/{id}/approve", null)).StatusCode);
        var other = Payload();
        other["name"] = "Another Proposal";
        var second = await Read(await client.PostAsJsonAsync("/api/agent-tools/execute", new { tool = "create_recipe_draft", arguments = other }));
        var otherId = second.GetProperty("result").GetProperty("data").GetProperty("actionId").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/agent-actions/{otherId}/reject", null)).StatusCode);
        var entries = audit.Messages.Where(x => x.StartsWith("Agent action audit:")).ToArray();
        foreach (var stage in new[] { "pending_action_created", "approval_requested", "approved", "execution_started", "execution_succeeded", "repeated_execution_attempt_rejected", "rejected" })
            Assert.Contains(entries, x => x.Contains("Stage=" + stage));
        Assert.All(entries, x => { Assert.Contains(userId, x); Assert.Contains("ActionId=", x); Assert.Contains("Timestamp=", x); Assert.Contains("Policy=RequireApproval", x); Assert.DoesNotContain("A simple dinner", x); Assert.DoesNotContain("Bearer", x); });
        Assert.Contains(audit.Messages, x => x.StartsWith("Write tool requested:"));
        Assert.Contains(audit.Messages, x => x.StartsWith("Tool policy evaluated:"));
    }

    private sealed class AuditedFactory(AuditProvider audit) : AgentApiFactory(new FakeAgentModel())
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureLogging(logging => logging.AddProvider(audit));
        }
    }

    private sealed class AuditProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new AuditLogger(Messages);
        public void Dispose() { }
        private sealed class AuditLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel level) => true;
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => messages.Enqueue(formatter(state, exception));
        }
    }

    private static JsonObject Payload() => JsonNode.Parse("""
    {
      "name":"  Simple   Chicken Pasta ","description":" A   simple dinner. ","category":" dinner ",
      "servings":4,"preparationTimeMinutes":15,"cookingTimeMinutes":25,
      "ingredients":[{"ingredient":" chicken breast ","quantity":500,"unit":"g"}],
      "steps":[{"stepNumber":2,"instruction":" Serve. "},{"stepNumber":1,"instruction":" Cook   safely. "}],
      "tools":["Pan"," Knife "]
    }
    """)!.AsObject();

    private static JsonArray Repeat(JsonNode value, int count) => new(Enumerable.Range(0, count).Select(_ => value.DeepClone()).ToArray());
    private static ClaimsPrincipal Principal(string role) => new(new ClaimsIdentity(new[]
    { new Claim(ClaimTypes.NameIdentifier, "trusted-id"), new Claim(ClaimTypes.Role, role) }, "test"));

    private static async Task<Guid> Propose(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/agent-tools/execute", new { tool = "create_recipe_draft", arguments = Payload() });
        var body = await Read(response);
        Assert.True(response.IsSuccessStatusCode, body.GetRawText());
        Assert.Equal("approval_required", body.GetProperty("result").GetProperty("code").GetString());
        return body.GetProperty("result").GetProperty("data").GetProperty("actionId").GetGuid();
    }

    private static async Task Seed(MealAppApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Categories.Add(new Category { Name = "Dinner" });
        db.Ingredients.Add(new Ingredient { Name = "Chicken Breast", IngredientCategory = new IngredientCategory { Name = "Meat" } });
        db.Units.Add(new Unit { Name = "Gram", ShortName = "g" });
        await db.SaveChangesAsync();
    }

    private static async Task<string> Authenticate(MealAppApiFactory factory, HttpClient client, string role)
    {
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"action-{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser { Email = email, UserName = email, Name = "Approval Test", EmailConfirmed = true };
        Assert.True((await manager.CreateAsync(user, "Pass123$")).Succeeded);
        Assert.True((await manager.AddToRoleAsync(user, role)).Succeeded);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Pass123$" });
        var json = await Read(login);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("result").GetProperty("token").GetString());
        return user.Id;
    }

    private static async Task<JsonElement> Read(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.Clone();
    }
}
