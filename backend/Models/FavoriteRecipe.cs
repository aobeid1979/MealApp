namespace MealApp.Models;

public class FavoriteRecipe
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    public int RecipeId { get; set; }

    public Recipe? Recipe { get; set; }

    public DateTime CreatedAt { get; set; }
}
