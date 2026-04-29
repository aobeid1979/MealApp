using System.ComponentModel.DataAnnotations;

namespace MealApp.DTOs;

public class UpdateRecipeDto
{
    [Required]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public int PreparationTimeMinutes { get; set; }

    public int CookingTimeMinutes { get; set; }

    public int Servings { get; set; }

    public int CategoryId { get; set; }
}
