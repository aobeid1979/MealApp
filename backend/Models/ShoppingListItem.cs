using Microsoft.EntityFrameworkCore;

namespace MealApp.Models;

public class ShoppingListItem
{
    public int Id { get; set; }

    public int ShoppingListId { get; set; }

    public ShoppingList? ShoppingList { get; set; }

    public int IngredientId { get; set; }

    public Ingredient? Ingredient { get; set; }

    [Precision(18, 2)]
    public decimal Quantity { get; set; }

    public int UnitId { get; set; }

    public Unit? Unit { get; set; }

    public bool IsChecked { get; set; }
}
