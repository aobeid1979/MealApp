namespace MealApp.DTOs;

public class CreateRecipeIngredientDto
{
    public int IngredientId { get; set; }

    public decimal Quantity { get; set; }

    public int UnitId { get; set; }
}
