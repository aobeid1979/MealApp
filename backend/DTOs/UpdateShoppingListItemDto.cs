using System.ComponentModel.DataAnnotations;

namespace MealApp.DTOs;

public class UpdateShoppingListItemDto
{
    [Range(0.01, double.MaxValue)]
    public decimal? Quantity { get; set; }

    [Range(1, int.MaxValue)]
    public int? UnitId { get; set; }
}
