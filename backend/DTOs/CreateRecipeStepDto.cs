using System.ComponentModel.DataAnnotations;

namespace MealApp.DTOs;

public class CreateRecipeStepDto
{
    public int StepNumber { get; set; }

    [Required]
    public string Instruction { get; set; } = string.Empty;
}
