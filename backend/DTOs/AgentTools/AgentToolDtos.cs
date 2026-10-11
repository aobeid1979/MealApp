using System.Text.Json;
using System.Text.Json.Serialization;
using MealApp.DTOs;

namespace MealApp.DTOs.AgentTools;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class ToolExecutionRequest
{
    public string? Tool { get; set; }
    public JsonElement Arguments { get; set; }
}

public record ToolExecutionResult(string Tool, string Code, object? Data, int StatusCode, IReadOnlyList<string> Errors);
public record ToolRecipeSummary(int Id, string Name, string? Category, string? Description, int PreparationTimeMinutes, int CookingTimeMinutes);
public record ToolIngredientSummary(int Id, string Name, string? IngredientCategory);
public record ToolRecipeStep(int StepNumber, string Instruction);
public record ToolRecipeDetails(int Id, string Name, string? Description, string? Category, int Servings,
    int PreparationTimeMinutes, int CookingTimeMinutes, IReadOnlyList<RecipeDetailsIngredientResponseDto> Ingredients, IReadOnlyList<ToolRecipeStep> Steps, IReadOnlyList<string> Tools);
