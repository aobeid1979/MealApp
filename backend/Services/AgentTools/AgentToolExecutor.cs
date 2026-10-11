using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using MealApp.Data;
using MealApp.DTOs.AgentTools;
using MealApp.DTOs.Import;
using MealApp.Services.AgentActions;
using Microsoft.EntityFrameworkCore;

namespace MealApp.Services.AgentTools;

public class AgentToolExecutor(ToolRegistry registry, RecipeReadService recipes, ApplicationDbContext context,
    IHttpContextAccessor httpContextAccessor, ILogger<AgentToolExecutor> logger, AgentToolPolicy policy, AgentActionService actions)
{
    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken ct = default)
    {
        var started = DateTimeOffset.UtcNow;
        var timer = Stopwatch.StartNew();
        var principal = httpContextAccessor.HttpContext?.User;
        var userId = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        ToolExecutionResult? result = null;
        var auditTool = "<unknown>";
        try
        {
            if (principal?.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(userId))
                return result = Failure(auditTool, "unauthorized", 401, "Authentication is required.");
            if (!principal.IsInRole("User") && !principal.IsInRole("Admin"))
                return result = Failure(auditTool, "forbidden", 403, "User or Admin role is required.");
            if (request.Tool is null || !registry.TryGet(request.Tool, out var definition))
                return result = Failure(auditTool, "unknown_tool", 400, "Tool is not registered.");
            auditTool = definition.Name;
            var errors = registry.Validate(definition, request.Arguments);
            if (errors.Count > 0)
                return result = new(auditTool, "invalid_arguments", null, 400, errors);

            if (definition.Capability == ToolCapability.Write)
                logger.LogInformation("Write tool requested: Tool={Tool} UserId={UserId} Timestamp={Timestamp}", auditTool, userId, DateTimeOffset.UtcNow);
            var decision = policy.Evaluate(definition, principal);
            logger.LogInformation("Tool policy evaluated: Tool={Tool} UserId={UserId} Timestamp={Timestamp} Policy={Policy}",
                auditTool, userId, DateTimeOffset.UtcNow, decision);
            if (decision == ToolPolicyDecision.Deny)
                return result = Failure(auditTool, "policy_denied", 403, "This tool is not allowed for your account.");
            if (decision == ToolPolicyDecision.RequireApproval)
            {
                var proposal = request.Arguments.Deserialize<RecipeImportItem>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
                var pending = await actions.ProposeAsync(proposal, ct);
                return result = new(auditTool, pending.StatusCode == 200 ? "approval_required" : "draft_validation_failed",
                    pending.Data, pending.StatusCode, pending.Errors);
            }
            if (definition.Capability != ToolCapability.Read)
                return result = Failure(auditTool, "policy_denied", 403, "Direct write execution is not allowed.");

            var arguments = request.Arguments;
            var query = Text(arguments, "query");
            var category = Text(arguments, "category");
            var limit = arguments.TryGetProperty("limit", out var limitValue) ? limitValue.GetInt32() : ToolRegistry.DefaultLimit;
            object? data;
            switch (definition.Name)
            {
                case "search_recipes":
                    data = await recipes.Search(query, category: category).OrderBy(x => x.Title).ThenBy(x => x.Id).Take(limit)
                        .Select(x => new ToolRecipeSummary(x.Id, x.Title, x.Category != null ? x.Category.Name : null,
                            x.Description, x.PreparationTimeMinutes, x.CookingTimeMinutes)).ToListAsync(ct);
                    break;
                case "list_ingredients":
                    var ingredientQuery = context.Ingredients.AsNoTracking();
                    if (query.Length > 0)
                    {
                        var text = query.ToLowerInvariant();
                        ingredientQuery = ingredientQuery.Where(x => x.Name.ToLower().Contains(text));
                    }
                    if (category.Length > 0)
                    {
                        var text = category.ToLowerInvariant();
                        ingredientQuery = ingredientQuery.Where(x => x.IngredientCategory != null && x.IngredientCategory.Name.ToLower() == text);
                    }
                    data = await ingredientQuery.OrderBy(x => x.Name).ThenBy(x => x.Id).Take(limit)
                        .Select(x => new ToolIngredientSummary(x.Id, x.Name, x.IngredientCategory != null ? x.IngredientCategory.Name : null))
                        .ToListAsync(ct);
                    break;
                case "get_recipe":
                    var recipe = await recipes.GetDetailsAsync(arguments.GetProperty("recipeId").GetInt32(), ct);
                    if (recipe is null) return result = Failure(auditTool, "not_found", 404, "Recipe not found.");
                    data = new ToolRecipeDetails(recipe.Id, recipe.Title, recipe.Description, recipe.CategoryName, recipe.Servings,
                        recipe.PreparationTimeMinutes, recipe.CookingTimeMinutes, recipe.Ingredients,
                        recipe.Steps.Select(x => new ToolRecipeStep(x.StepNumber, x.Instruction)).ToList(),
                        recipe.Tools.Select(x => x.Name).ToList());
                    break;
                case "get_recipe_ingredients":
                    var id = arguments.GetProperty("recipeId").GetInt32();
                    if (!await recipes.Search().AnyAsync(x => x.Id == id, ct))
                        return result = Failure(auditTool, "not_found", 404, "Recipe not found.");
                    data = await recipes.Ingredients(id).ToListAsync(ct);
                    break;
                default:
                    return result = Failure(auditTool, "unknown_tool", 400, "Tool has no registered executor.");
            }
            return result = new(auditTool, "success", data, 200, []);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            result = Failure(auditTool, "cancelled", 499, "Tool execution cancelled.");
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Read-only tool {Tool} execution failed.", auditTool);
            return result = Failure(auditTool, "execution_failed", 500, "Unable to execute the tool. Please try again.");
        }
        finally
        {
            logger.LogInformation("Agent tool audit: Tool={Tool} UserId={UserId} Timestamp={Timestamp} Success={Success} Code={Code} ValidationFailure={ValidationFailure} DurationMs={DurationMs}",
                auditTool, userId, started, result?.StatusCode == 200, result?.Code ?? "failed",
                result?.Code is "invalid_arguments" or "unknown_tool", timer.Elapsed.TotalMilliseconds);
        }
    }

    private static string Text(JsonElement arguments, string name)
        => arguments.TryGetProperty(name, out var value) ? value.GetString()!.Trim() : "";
    private static ToolExecutionResult Failure(string tool, string code, int status, string error)
        => new(tool, code, null, status, [error]);
}
