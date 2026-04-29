using System.ComponentModel.DataAnnotations;

namespace MealApp.DTOs;

public class UpdateCategoryDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}
