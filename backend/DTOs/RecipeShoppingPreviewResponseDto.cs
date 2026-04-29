namespace MealApp.DTOs;

public class RecipeShoppingPreviewResponseDto
{
    public string? IngredientName { get; set; }

    public decimal Quantity { get; set; }

    public string? UnitName { get; set; }

    public string? UnitShortName { get; set; }
}
