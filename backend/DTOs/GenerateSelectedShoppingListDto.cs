using System.ComponentModel.DataAnnotations;

namespace MealApp.DTOs;

public class GenerateSelectedShoppingListDto
{
    [Required]
    [MinLength(1)]
    public List<int> MealPlanIds { get; set; } = [];

    [MaxLength(100)]
    public string? Name { get; set; }
}
