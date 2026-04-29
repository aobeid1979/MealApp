using System.ComponentModel.DataAnnotations;

namespace MealApp.Models;

public class IngredientCategory
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public ICollection<Ingredient> Ingredients { get; set; } = new List<Ingredient>();
}
