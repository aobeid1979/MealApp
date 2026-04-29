using MealApp.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MealApp.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Category> Categories { get; set; }

    public DbSet<Ingredient> Ingredients { get; set; }

    public DbSet<IngredientCategory> IngredientCategories { get; set; }

    public DbSet<Unit> Units { get; set; }

    public DbSet<Recipe> Recipes { get; set; }

    public DbSet<RecipeIngredient> RecipeIngredients { get; set; }

    public DbSet<RecipeStep> RecipeSteps { get; set; }

    public DbSet<RecipeTool> RecipeTools { get; set; }

    public DbSet<MealPlan> MealPlans { get; set; }

    public DbSet<ShoppingList> ShoppingLists { get; set; }

    public DbSet<ShoppingListItem> ShoppingListItems { get; set; }

    public DbSet<FavoriteRecipe> FavoriteRecipes { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Ingredient>()
            .HasOne(ingredient => ingredient.IngredientCategory)
            .WithMany(ingredientCategory => ingredientCategory.Ingredients)
            .HasForeignKey(ingredient => ingredient.IngredientCategoryId)
            .IsRequired();

        builder.Entity<FavoriteRecipe>()
            .HasOne(favoriteRecipe => favoriteRecipe.User)
            .WithMany(user => user.FavoriteRecipes)
            .HasForeignKey(favoriteRecipe => favoriteRecipe.UserId)
            .IsRequired();

        builder.Entity<FavoriteRecipe>()
            .HasOne(favoriteRecipe => favoriteRecipe.Recipe)
            .WithMany(recipe => recipe.FavoriteRecipes)
            .HasForeignKey(favoriteRecipe => favoriteRecipe.RecipeId)
            .IsRequired();

        builder.Entity<FavoriteRecipe>()
            .HasIndex(favoriteRecipe => new
            {
                favoriteRecipe.UserId,
                favoriteRecipe.RecipeId
            })
            .IsUnique();
    }
}
