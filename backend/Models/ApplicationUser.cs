using Microsoft.AspNetCore.Identity;

namespace MealApp.Models;

public class ApplicationUser : IdentityUser
{
    public string Name { get; set; } = string.Empty;

    public ICollection<MealPlan> MealPlans { get; set; } = new List<MealPlan>();

    public ICollection<ShoppingList> ShoppingLists { get; set; } = new List<ShoppingList>();

    public ICollection<FavoriteRecipe> FavoriteRecipes { get; set; } = new List<FavoriteRecipe>();
}
