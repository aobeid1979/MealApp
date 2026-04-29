using System.ComponentModel.DataAnnotations;

namespace MealApp.Models;

public class Recipe
{
    public int Id { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public int PreparationTimeMinutes { get; set; }

    public int CookingTimeMinutes { get; set; }

    public int Servings { get; set; }

    public int CategoryId { get; set; }

    public Category? Category { get; set; }

    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();

    public ICollection<RecipeStep> RecipeSteps { get; set; } = new List<RecipeStep>();

    public ICollection<RecipeTool> RecipeTools { get; set; } = new List<RecipeTool>();

    public ICollection<MealPlan> MealPlans { get; set; } = new List<MealPlan>();

    public ICollection<FavoriteRecipe> FavoriteRecipes { get; set; } = new List<FavoriteRecipe>();
}
