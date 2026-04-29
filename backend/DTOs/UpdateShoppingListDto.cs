using System.ComponentModel.DataAnnotations;

namespace MealApp.DTOs;

public class UpdateShoppingListDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
}
