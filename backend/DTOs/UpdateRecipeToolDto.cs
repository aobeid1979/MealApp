using System.ComponentModel.DataAnnotations;

namespace MealApp.DTOs;

public class UpdateRecipeToolDto
{
    [Required]
    public string Name { get; set; } = string.Empty;
}
