namespace MealApp.DTOs;

public class IngredientResponseDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public int IngredientCategoryId { get; set; }

    public string? IngredientCategoryName { get; set; }
}
