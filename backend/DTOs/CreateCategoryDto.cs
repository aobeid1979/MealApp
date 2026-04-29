using System.ComponentModel.DataAnnotations;

namespace MealApp.DTOs;

public class CreateCategoryDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}
