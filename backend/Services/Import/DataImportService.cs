using System.Data;
using System.Text.Json;
using MealApp.DTOs.AgentActions;
using MealApp.Data;
using MealApp.DTOs.Import;
using MealApp.Models;
using Microsoft.EntityFrameworkCore;

namespace MealApp.Services.Import;

public class DataImportService(ApplicationDbContext context)
{
    public Task<ImportResult> IngredientsAsync(List<IngredientImportItem?> items, bool preview, CancellationToken ct)
        => ExecuteAsync(preview, async () =>
        {
            var categories = await context.IngredientCategories.AsNoTracking().ToListAsync(ct);
            var existing = await context.Ingredients.AsNoTracking().ToListAsync(ct);
            var names = existing.Select(x => Normalize(x.Name)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var result = NewResult(items.Count, preview);
            for (var i = 0; i < items.Count; i++)
            {
                var row = i + 1;
                var item = items[i];
                var name = Normalize(item?.Name);
                var errors = new List<ImportValidationError>();
                Text(errors, row, "name", name, 200, true);
                Text(errors, row, "description", Normalize(item?.Description), 5000);
                Image(errors, row, item?.ImageUrl);
                var category = Resolve(categories, x => x.Name, item?.IngredientCategory,
                    errors, row, "ingredientCategory");
                if (Invalid(result, errors, row, name)) continue;
                if (!names.Add(name)) { Duplicate(result, row, name); continue; }
                var entity = new Ingredient
                {
                    Name = name, Description = Optional(item!.Description),
                    ImageUrl = Optional(item.ImageUrl), IngredientCategoryId = category!.Id
                };
                if (!preview)
                {
                    context.Ingredients.Add(entity);
                    await context.SaveChangesAsync(ct);
                }
                Accepted(result, row, name, preview ? null : entity.Id);
            }
            return result;
        }, ct);

    public Task<ImportResult> RecipesAsync(List<RecipeImportItem?> items, bool preview, CancellationToken ct)
        => RecipesCoreAsync(items, preview, ct);

    public async Task<RecipeDraftValidation> ValidateDraftAsync(RecipeImportItem item, CancellationToken ct)
    {
        PreparedRecipeDraft? draft = null;
        var report = await RecipesCoreAsync([item], true, ct, onPrepared: (recipe, normalized) =>
            draft = new(normalized, ReferenceBinding(recipe)));
        return new(report, draft);
    }

    public Task<ImportResult> ExecuteApprovedDraftAsync(PreparedRecipeDraft draft, CancellationToken ct)
        => RecipesCoreAsync([draft.Payload], false, ct, expectedBinding: draft.ReferenceBinding);

    private Task<ImportResult> RecipesCoreAsync(List<RecipeImportItem?> items, bool preview, CancellationToken ct,
        Action<Recipe, RecipeImportItem>? onPrepared = null, string? expectedBinding = null)
        => ExecuteAsync(preview, async () =>
        {
            var categories = await context.Categories.AsNoTracking().ToListAsync(ct);
            var ingredients = await context.Ingredients.AsNoTracking().ToListAsync(ct);
            var units = await context.Units.AsNoTracking().ToListAsync(ct);
            var titles = (await context.Recipes.AsNoTracking().Select(x => x.Title).ToListAsync(ct))
                .Select(Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var result = NewResult(items.Count, preview);
            for (var i = 0; i < items.Count; i++)
            {
                var row = i + 1;
                var item = items[i];
                var name = Normalize(item?.Name);
                var errors = new List<ImportValidationError>();
                Text(errors, row, "name", name, 200, true);
                Text(errors, row, "description", Normalize(item?.Description), 5000);
                Image(errors, row, item?.ImageUrl);
                var category = Resolve(categories, x => x.Name, item?.Category, errors, row, "category");
                if (item is null)
                {
                    Invalid(result, errors, row, name);
                    continue;
                }
                if (item.Servings is < 1 or > 50) Error(errors, row, "servings", "Servings must be between 1 and 50.");
                if (item.PreparationTimeMinutes is < 0 or > 10080 || item.CookingTimeMinutes is < 0 or > 10080)
                    Error(errors, row, "time", "Times must be between 0 and 10080 minutes.");
                var recipe = new Recipe
                {
                    Title = name, Description = Optional(item.Description), ImageUrl = Optional(item.ImageUrl),
                    CategoryId = category?.Id ?? 0, Servings = item.Servings,
                    PreparationTimeMinutes = item.PreparationTimeMinutes, CookingTimeMinutes = item.CookingTimeMinutes
                };
                if (item.Ingredients is not { Count: > 0 and <= 100 })
                    Error(errors, row, "ingredients", "Provide between 1 and 100 ingredients.");
                else
                {
                    var used = new HashSet<int>();
                    for (var j = 0; j < item.Ingredients.Count; j++)
                    {
                        var entry = item.Ingredients[j];
                        var field = $"ingredients[{j}]";
                        var ingredient = Resolve(ingredients, x => x.Name, entry?.Ingredient, errors, row, field + ".ingredient");
                        var unitName = Normalize(entry?.Unit);
                        var matches = units.Where(x => Same(x.Name, unitName) || Same(x.ShortName, unitName)).ToList();
                        var unit = matches.Count == 1 ? matches[0] : null;
                        if (unit is null) Error(errors, row, field + ".unit", "Unit must match exactly one existing unit name or short name.");
                        var quantity = entry?.Quantity ?? 0;
                        if (quantity <= 0 || quantity > 9999999999999999.99m || decimal.Round(quantity, 2) != quantity)
                            Error(errors, row, field + ".quantity", "Quantity must be positive and fit decimal(18,2), with at most two decimal places.");
                        if (ingredient is not null && !used.Add(ingredient.Id))
                            Error(errors, row, field, "Ingredient already occurs in this recipe.");
                        if (ingredient is not null && unit is not null)
                            recipe.RecipeIngredients.Add(new RecipeIngredient { IngredientId = ingredient.Id, UnitId = unit.Id, Quantity = quantity });
                    }
                }
                if (item.Steps is not { Count: > 0 and <= 100 })
                    Error(errors, row, "steps", "Provide between 1 and 100 steps.");
                else
                {
                    var numbers = new HashSet<int>();
                    foreach (var step in item.Steps)
                    {
                        Text(errors, row, "steps.instruction", Normalize(step?.Instruction), 5000, true);
                        if (step is null || step.StepNumber <= 0 || !numbers.Add(step.StepNumber))
                            Error(errors, row, "steps.stepNumber", "Step numbers must be positive and unique.");
                        else recipe.RecipeSteps.Add(new RecipeStep { StepNumber = step.StepNumber, Instruction = Normalize(step.Instruction) });
                    }
                }
                if (item.Tools?.Count > 100) Error(errors, row, "tools", "At most 100 tools are allowed.");
                else if (item.Tools is not null)
                {
                    var tools = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var tool in item.Tools)
                    {
                        var toolName = Normalize(tool);
                        Text(errors, row, "tools", toolName, 200, true);
                        if (!tools.Add(toolName)) Error(errors, row, "tools", "Tool names must be unique.");
                        recipe.RecipeTools.Add(new RecipeTool { Name = toolName });
                    }
                }
                if (expectedBinding is not null && ReferenceBinding(recipe) != expectedBinding)
                    Error(errors, row, "references", "Recipe references have changed since the proposal was created. Create a new proposal.");
                if (Invalid(result, errors, row, name)) continue;
                if (!titles.Add(name)) { Duplicate(result, row, name); continue; }
                onPrepared?.Invoke(recipe, new RecipeImportItem
                {
                    Name = recipe.Title, Description = recipe.Description, ImageUrl = recipe.ImageUrl,
                    Category = Normalize(category!.Name), Servings = recipe.Servings,
                    PreparationTimeMinutes = recipe.PreparationTimeMinutes, CookingTimeMinutes = recipe.CookingTimeMinutes,
                    Ingredients = recipe.RecipeIngredients.Select(x => (RecipeImportIngredient?)new RecipeImportIngredient
                    {
                        Ingredient = Normalize(ingredients.Single(y => y.Id == x.IngredientId).Name),
                        Quantity = x.Quantity, Unit = Normalize(units.Single(y => y.Id == x.UnitId).Name)
                    }).ToList(),
                    Steps = recipe.RecipeSteps.OrderBy(x => x.StepNumber).Select(x => (RecipeImportStep?)new RecipeImportStep
                    { StepNumber = x.StepNumber, Instruction = x.Instruction }).ToList(),
                    Tools = recipe.RecipeTools.OrderBy(x => x.Name, StringComparer.Ordinal).Select(x => (string?)x.Name).ToList()
                });
                if (!preview)
                {
                    // One graph/save keeps a recipe and its children together.
                    context.Recipes.Add(recipe);
                    await context.SaveChangesAsync(ct);
                }
                Accepted(result, row, name, preview ? null : recipe.Id);
            }
            return result;
        }, ct);

    private async Task<ImportResult> ExecuteAsync(bool preview, Func<Task<ImportResult>> action, CancellationToken ct)
    {
        if (preview || !context.Database.IsRelational()) return await action();
        // SQL Server retries must replay the whole transaction, including validation.
        return await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var result = await action();
            await transaction.CommitAsync(ct);
            return result;
        });
    }

    private static string Normalize(string? value)
        => string.Join(" ", (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    private static string ReferenceBinding(Recipe recipe) => JsonSerializer.Serialize(new
    {
        recipe.CategoryId,
        Ingredients = recipe.RecipeIngredients.OrderBy(x => x.IngredientId).Select(x => new { x.IngredientId, x.UnitId, x.Quantity })
    });
    private static string? Optional(string? value) => Normalize(value) is { Length: > 0 } text ? text : null;
    private static bool Same(string value, string other) => string.Equals(Normalize(value), other, StringComparison.OrdinalIgnoreCase);

    private static T? Resolve<T>(List<T> values, Func<T, string> getName, string? name,
        List<ImportValidationError> errors, int row, string field) where T : class
    {
        var normalized = Normalize(name);
        var matches = values.Where(x => Same(getName(x), normalized)).ToList();
        if (normalized.Length > 0 && matches.Count == 1) return matches[0];
        Error(errors, row, field, "Reference must match exactly one existing record by name.");
        return null;
    }

    private static void Text(List<ImportValidationError> errors, int row, string field, string text, int max, bool required = false)
    {
        if (required && text.Length == 0) Error(errors, row, field, "This field is required.");
        if (text.Length > max) Error(errors, row, field, $"Maximum length is {max} characters.");
    }
    private static void Image(List<ImportValidationError> errors, int row, string? value)
    {
        var text = Optional(value);
        if (text is not null && (text.Length > 2048 || !Uri.TryCreate(text, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
            Error(errors, row, "imageUrl", "Use an HTTP/HTTPS image URL of at most 2048 characters.");
    }
    private static void Error(List<ImportValidationError> errors, int row, string field, string message)
        => errors.Add(new(row, field, message));
    private static ImportResult NewResult(int count, bool preview) => new() { TotalReceived = count, IsPreview = preview };
    private static bool Invalid(ImportResult result, List<ImportValidationError> errors, int row, string name)
    {
        if (errors.Count == 0) return false;
        result.ValidationErrors.AddRange(errors);
        result.Skipped++;
        result.Rows.Add(new(row, name, "invalid"));
        return true;
    }
    private static void Duplicate(ImportResult result, int row, string name)
    {
        result.Duplicates++;
        result.Skipped++;
        result.Rows.Add(new(row, name, "duplicate"));
    }
    private static void Accepted(ImportResult result, int row, string name, int? id)
    {
        result.Ready++;
        if (!result.IsPreview) result.Imported++;
        result.Rows.Add(new(row, name, result.IsPreview ? "ready" : "imported", id));
    }
}
