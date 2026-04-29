using System.ComponentModel.DataAnnotations;

namespace MealApp.Models;

public class RecipeTool
{
    public int Id { get; set; }

    public int RecipeId { get; set; }

    public Recipe? Recipe { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;
}
