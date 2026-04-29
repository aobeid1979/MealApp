using System.ComponentModel.DataAnnotations;

namespace MealApp.DTOs;

public class CreateShoppingListItemDto
{
    [Range(1, int.MaxValue)]
    public int IngredientId { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal Quantity { get; set; }

    [Range(1, int.MaxValue)]
    public int UnitId { get; set; }
}
