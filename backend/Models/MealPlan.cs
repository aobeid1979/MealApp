namespace MealApp.Models;

public class MealPlan
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    public int RecipeId { get; set; }

    public Recipe? Recipe { get; set; }

    public int Servings { get; set; }

    public DateTime PlannedDate { get; set; }

    public DateTime CreatedAt { get; set; }
}
