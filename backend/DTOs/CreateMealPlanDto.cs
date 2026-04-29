using System.ComponentModel.DataAnnotations;

namespace MealApp.DTOs;

public class CreateMealPlanDto
{
    [Range(1, int.MaxValue)]
    public int RecipeId { get; set; }

    [Range(1, 50)]
    public int Servings { get; set; }

    [Required]
    public DateTime PlannedDate { get; set; }
}
