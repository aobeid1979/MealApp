using System.ComponentModel.DataAnnotations;

namespace MealApp.Models;

public class RecipeStep
{
    public int Id { get; set; }

    public int RecipeId { get; set; }

    public Recipe? Recipe { get; set; }

    public int StepNumber { get; set; }

    [Required]
    public string Instruction { get; set; } = string.Empty;
}
