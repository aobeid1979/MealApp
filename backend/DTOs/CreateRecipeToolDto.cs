using System.ComponentModel.DataAnnotations;

namespace MealApp.DTOs;

public class CreateRecipeToolDto
{
    [Required]
    public string Name { get; set; } = string.Empty;
}
