namespace MealApp.DTOs;

public class ShoppingListItemResponseDto
{
    public int Id { get; set; }

    public int? IngredientId { get; set; }

    public string? IngredientName { get; set; }

    public string? CustomName { get; set; }

    public string? ItemName { get; set; }

    public int? IngredientCategoryId { get; set; }

    public string? IngredientCategoryName { get; set; }

    public decimal? Quantity { get; set; }

    public int? UnitId { get; set; }

    public string? UnitName { get; set; }

    public string? UnitShortName { get; set; }

    public bool IsChecked { get; set; }

    public bool IsGenerated { get; set; }
}
