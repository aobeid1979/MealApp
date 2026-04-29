namespace MealApp.DTOs;

public class RecipeDetailsIngredientResponseDto
{
    public int IngredientId { get; set; }

    public string? IngredientName { get; set; }

    public decimal Quantity { get; set; }

    public int UnitId { get; set; }

    public string? UnitName { get; set; }

    public string? UnitShortName { get; set; }
}
