using System.ComponentModel.DataAnnotations;

namespace MealApp.DTOs;

public class CreateIngredientCategoryDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }
}
