namespace MealApp.DTOs;

public class RecipeIngredientResponseDto
{
    public int Id { get; set; }

    public int RecipeId { get; set; }

    public int IngredientId { get; set; }

    public string? IngredientName { get; set; }

    public decimal Quantity { get; set; }

    public int UnitId { get; set; }

    public string? UnitName { get; set; }

    public string? UnitShortName { get; set; }
}
