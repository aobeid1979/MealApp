using System.ComponentModel.DataAnnotations;

namespace MealApp.Models;

public class Ingredient
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    [Required]
    public int IngredientCategoryId { get; set; }

    public IngredientCategory? IngredientCategory { get; set; }

    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();
}
