using System.ComponentModel.DataAnnotations;

namespace MealApp.DTOs;

public class UpdateShoppingListItemsCheckStateDto
{
    [Required]
    public bool IsChecked { get; set; }
}
