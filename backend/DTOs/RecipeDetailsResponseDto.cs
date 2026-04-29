namespace MealApp.DTOs;

public class RecipeDetailsResponseDto
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public int PreparationTimeMinutes { get; set; }

    public int CookingTimeMinutes { get; set; }

    public int Servings { get; set; }

    public int CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public List<RecipeDetailsIngredientResponseDto> Ingredients { get; set; } = [];

    public List<RecipeStepResponseDto> Steps { get; set; } = [];

    public List<RecipeToolResponseDto> Tools { get; set; } = [];
}
