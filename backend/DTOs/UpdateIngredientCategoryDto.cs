using System.ComponentModel.DataAnnotations;

namespace MealApp.DTOs;

public class UpdateIngredientCategoryDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }
}
