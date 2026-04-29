namespace MealApp.DTOs;

public class FavoriteRecipeResponseDto
{
    public int Id { get; set; }

    public int RecipeId { get; set; }

    public string? RecipeTitle { get; set; }

    public string? RecipeDescription { get; set; }

    public string? RecipeImageUrl { get; set; }

    public int PreparationTimeMinutes { get; set; }

    public int CookingTimeMinutes { get; set; }

    public int Servings { get; set; }

    public int CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public DateTime CreatedAt { get; set; }
}
