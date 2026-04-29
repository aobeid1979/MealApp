using System.ComponentModel.DataAnnotations;

namespace MealApp.DTOs;

public class CreateIngredientDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    [Required]
    public int IngredientCategoryId { get; set; }
}
