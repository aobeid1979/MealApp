namespace MealApp.DTOs;

public class MealPlanResponseDto
{
    public int Id { get; set; }

    public int RecipeId { get; set; }

    public string? RecipeTitle { get; set; }

    public string? RecipeImageUrl { get; set; }

    public int Servings { get; set; }

    public DateTime PlannedDate { get; set; }

    public DateTime CreatedAt { get; set; }
}
