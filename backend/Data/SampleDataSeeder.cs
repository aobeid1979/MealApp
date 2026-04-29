using MealApp.Models;
using Microsoft.EntityFrameworkCore;

namespace MealApp.Data;

public static class SampleDataSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

        var categories = await SeedCategoriesAsync(context);
        var ingredientCategories = await SeedIngredientCategoriesAsync(context);
        var units = await SeedUnitsAsync(context);
        var ingredients = await SeedIngredientsAsync(context, ingredientCategories);

        await SeedRecipesAsync(context, categories, ingredients, units);
    }

    private static async Task<Dictionary<string, Category>> SeedCategoriesAsync(ApplicationDbContext context)
    {
        var categories = await context.Categories.ToListAsync();

        AddIfMissing(context, categories, "Pasta", "Comforting noodle dishes and sauces.");
        AddIfMissing(context, categories, "Chicken", "Simple chicken meals for lunch or dinner.");
        AddIfMissing(context, categories, "Salad", "Fresh bowls, greens, and light meals.");
        AddIfMissing(context, categories, "Dessert", "Sweet recipes for after meals.");

        await context.SaveChangesAsync();

        return categories.ToDictionary(category => category.Name, StringComparer.OrdinalIgnoreCase);

        static void AddIfMissing(
            ApplicationDbContext context,
            List<Category> categories,
            string name,
            string description)
        {
            if (categories.Any(category => NamesMatch(category.Name, name)))
            {
                return;
            }

            var category = new Category
            {
                Name = name,
                Description = description
            };

            context.Categories.Add(category);
            categories.Add(category);
        }
    }

    private static async Task<Dictionary<string, IngredientCategory>> SeedIngredientCategoriesAsync(
        ApplicationDbContext context)
    {
        var ingredientCategories = await context.IngredientCategories.ToListAsync();

        AddIfMissing(context, ingredientCategories, "Grains and Pasta", "Pasta, rice, grains, and similar staples.");
        AddIfMissing(context, ingredientCategories, "Meat and Poultry", "Chicken, beef, and other proteins.");
        AddIfMissing(context, ingredientCategories, "Vegetables", "Fresh vegetables and herbs.");
        AddIfMissing(context, ingredientCategories, "Dairy", "Milk, cheese, yogurt, and cream.");
        AddIfMissing(context, ingredientCategories, "Pantry", "Oil, spices, sauces, and shelf-stable items.");
        AddIfMissing(context, ingredientCategories, "Fruit", "Fresh fruit and berries.");
        AddIfMissing(context, ingredientCategories, "Baking", "Chocolate, sugar, flour, and baking staples.");

        await context.SaveChangesAsync();

        return ingredientCategories.ToDictionary(category => category.Name, StringComparer.OrdinalIgnoreCase);

        static void AddIfMissing(
            ApplicationDbContext context,
            List<IngredientCategory> ingredientCategories,
            string name,
            string description)
        {
            if (ingredientCategories.Any(category => NamesMatch(category.Name, name)))
            {
                return;
            }

            var ingredientCategory = new IngredientCategory
            {
                Name = name,
                Description = description
            };

            context.IngredientCategories.Add(ingredientCategory);
            ingredientCategories.Add(ingredientCategory);
        }
    }

    private static async Task<Dictionary<string, Unit>> SeedUnitsAsync(ApplicationDbContext context)
    {
        var units = await context.Units.ToListAsync();

        AddIfMissing(context, units, "Gram", "g");
        AddIfMissing(context, units, "Milliliter", "ml");
        AddIfMissing(context, units, "Piece", "pcs");
        AddIfMissing(context, units, "Tablespoon", "tbsp");
        AddIfMissing(context, units, "Teaspoon", "tsp");
        AddIfMissing(context, units, "Cup", "cup");

        await context.SaveChangesAsync();

        return units.ToDictionary(unit => unit.Name, StringComparer.OrdinalIgnoreCase);

        static void AddIfMissing(ApplicationDbContext context, List<Unit> units, string name, string shortName)
        {
            if (units.Any(unit => NamesMatch(unit.Name, name)))
            {
                return;
            }

            var unit = new Unit
            {
                Name = name,
                ShortName = shortName
            };

            context.Units.Add(unit);
            units.Add(unit);
        }
    }

    private static async Task<Dictionary<string, Ingredient>> SeedIngredientsAsync(
        ApplicationDbContext context,
        Dictionary<string, IngredientCategory> ingredientCategories)
    {
        var ingredients = await context.Ingredients.ToListAsync();

        AddIfMissing(context, ingredients, ingredientCategories, "Spaghetti", "Dry spaghetti pasta.", "Grains and Pasta");
        AddIfMissing(context, ingredients, ingredientCategories, "Tomato Sauce", "Smooth tomato cooking sauce.", "Pantry");
        AddIfMissing(context, ingredients, ingredientCategories, "Parmesan Cheese", "Grated parmesan cheese.", "Dairy");
        AddIfMissing(context, ingredients, ingredientCategories, "Chicken Breast", "Boneless chicken breast.", "Meat and Poultry");
        AddIfMissing(context, ingredients, ingredientCategories, "Lemon", "Fresh lemon.", "Fruit");
        AddIfMissing(context, ingredients, ingredientCategories, "Olive Oil", "Extra virgin olive oil.", "Pantry");
        AddIfMissing(context, ingredients, ingredientCategories, "Mixed Greens", "Fresh salad greens.", "Vegetables");
        AddIfMissing(context, ingredients, ingredientCategories, "Cucumber", "Fresh cucumber.", "Vegetables");
        AddIfMissing(context, ingredients, ingredientCategories, "Chickpeas", "Cooked chickpeas.", "Pantry");
        AddIfMissing(context, ingredients, ingredientCategories, "Greek Yogurt", "Plain Greek yogurt.", "Dairy");
        AddIfMissing(context, ingredients, ingredientCategories, "Dark Chocolate", "Chopped dark chocolate.", "Baking");
        AddIfMissing(context, ingredients, ingredientCategories, "Mixed Berries", "Strawberries, blueberries, or raspberries.", "Fruit");

        await context.SaveChangesAsync();

        return ingredients.ToDictionary(ingredient => ingredient.Name, StringComparer.OrdinalIgnoreCase);

        static void AddIfMissing(
            ApplicationDbContext context,
            List<Ingredient> ingredients,
            Dictionary<string, IngredientCategory> ingredientCategories,
            string name,
            string description,
            string ingredientCategoryName)
        {
            if (ingredients.Any(ingredient => NamesMatch(ingredient.Name, name)))
            {
                return;
            }

            var ingredient = new Ingredient
            {
                Name = name,
                Description = description,
                IngredientCategoryId = ingredientCategories[ingredientCategoryName].Id
            };

            context.Ingredients.Add(ingredient);
            ingredients.Add(ingredient);
        }
    }

    private static async Task SeedRecipesAsync(
        ApplicationDbContext context,
        Dictionary<string, Category> categories,
        Dictionary<string, Ingredient> ingredients,
        Dictionary<string, Unit> units)
    {
        var existingRecipeTitles = await context.Recipes
            .Select(recipe => recipe.Title)
            .ToListAsync();

        if (!existingRecipeTitles.Any(title => NamesMatch(title, "Creamy Tomato Pasta")))
        {
            context.Recipes.Add(new Recipe
            {
                Title = "Creamy Tomato Pasta",
                Description = "A quick pasta dinner with tomato sauce and parmesan.",
                ImageUrl = "https://images.unsplash.com/photo-1621996346565-e3dbc646d9a9",
                PreparationTimeMinutes = 10,
                CookingTimeMinutes = 20,
                Servings = 4,
                CategoryId = categories["Pasta"].Id,
                RecipeIngredients =
                [
                    CreateRecipeIngredient(ingredients["Spaghetti"], units["Gram"], 400),
                    CreateRecipeIngredient(ingredients["Tomato Sauce"], units["Milliliter"], 500),
                    CreateRecipeIngredient(ingredients["Parmesan Cheese"], units["Gram"], 60)
                ],
                RecipeSteps =
                [
                    CreateRecipeStep(1, "Cook the spaghetti in salted water until al dente."),
                    CreateRecipeStep(2, "Warm the tomato sauce in a pan."),
                    CreateRecipeStep(3, "Toss pasta with sauce and finish with parmesan.")
                ],
                RecipeTools =
                [
                    CreateRecipeTool("Large pot"),
                    CreateRecipeTool("Saucepan"),
                    CreateRecipeTool("Colander")
                ]
            });
        }

        if (!existingRecipeTitles.Any(title => NamesMatch(title, "Lemon Herb Chicken")))
        {
            context.Recipes.Add(new Recipe
            {
                Title = "Lemon Herb Chicken",
                Description = "Juicy chicken breast with lemon and olive oil.",
                ImageUrl = "https://images.unsplash.com/photo-1598515214211-89d3c73ae83b",
                PreparationTimeMinutes = 15,
                CookingTimeMinutes = 25,
                Servings = 4,
                CategoryId = categories["Chicken"].Id,
                RecipeIngredients =
                [
                    CreateRecipeIngredient(ingredients["Chicken Breast"], units["Gram"], 700),
                    CreateRecipeIngredient(ingredients["Lemon"], units["Piece"], 1),
                    CreateRecipeIngredient(ingredients["Olive Oil"], units["Tablespoon"], 2)
                ],
                RecipeSteps =
                [
                    CreateRecipeStep(1, "Coat chicken with olive oil and lemon juice."),
                    CreateRecipeStep(2, "Season and bake until cooked through."),
                    CreateRecipeStep(3, "Rest for five minutes before slicing.")
                ],
                RecipeTools =
                [
                    CreateRecipeTool("Baking dish"),
                    CreateRecipeTool("Knife"),
                    CreateRecipeTool("Cutting board")
                ]
            });
        }

        if (!existingRecipeTitles.Any(title => NamesMatch(title, "Garden Chickpea Salad")))
        {
            context.Recipes.Add(new Recipe
            {
                Title = "Garden Chickpea Salad",
                Description = "A fresh salad with greens, cucumber, chickpeas, and lemon dressing.",
                ImageUrl = "https://images.unsplash.com/photo-1512621776951-a57141f2eefd",
                PreparationTimeMinutes = 15,
                CookingTimeMinutes = 0,
                Servings = 2,
                CategoryId = categories["Salad"].Id,
                RecipeIngredients =
                [
                    CreateRecipeIngredient(ingredients["Mixed Greens"], units["Gram"], 120),
                    CreateRecipeIngredient(ingredients["Cucumber"], units["Piece"], 1),
                    CreateRecipeIngredient(ingredients["Chickpeas"], units["Gram"], 240),
                    CreateRecipeIngredient(ingredients["Olive Oil"], units["Tablespoon"], 2),
                    CreateRecipeIngredient(ingredients["Lemon"], units["Piece"], 1)
                ],
                RecipeSteps =
                [
                    CreateRecipeStep(1, "Slice the cucumber and rinse the chickpeas."),
                    CreateRecipeStep(2, "Combine greens, cucumber, and chickpeas in a bowl."),
                    CreateRecipeStep(3, "Dress with olive oil and lemon juice.")
                ],
                RecipeTools =
                [
                    CreateRecipeTool("Mixing bowl"),
                    CreateRecipeTool("Knife"),
                    CreateRecipeTool("Salad tongs")
                ]
            });
        }

        if (!existingRecipeTitles.Any(title => NamesMatch(title, "Chocolate Berry Yogurt Parfait")))
        {
            context.Recipes.Add(new Recipe
            {
                Title = "Chocolate Berry Yogurt Parfait",
                Description = "A simple layered dessert with yogurt, berries, and dark chocolate.",
                ImageUrl = "https://images.unsplash.com/photo-1488477181946-6428a0291777",
                PreparationTimeMinutes = 10,
                CookingTimeMinutes = 0,
                Servings = 2,
                CategoryId = categories["Dessert"].Id,
                RecipeIngredients =
                [
                    CreateRecipeIngredient(ingredients["Greek Yogurt"], units["Gram"], 300),
                    CreateRecipeIngredient(ingredients["Mixed Berries"], units["Gram"], 200),
                    CreateRecipeIngredient(ingredients["Dark Chocolate"], units["Gram"], 40)
                ],
                RecipeSteps =
                [
                    CreateRecipeStep(1, "Spoon yogurt into serving glasses."),
                    CreateRecipeStep(2, "Add berries in layers."),
                    CreateRecipeStep(3, "Top with chopped dark chocolate.")
                ],
                RecipeTools =
                [
                    CreateRecipeTool("Serving glasses"),
                    CreateRecipeTool("Spoon"),
                    CreateRecipeTool("Knife")
                ]
            });
        }

        await context.SaveChangesAsync();
    }

    private static RecipeIngredient CreateRecipeIngredient(Ingredient ingredient, Unit unit, decimal quantity)
    {
        return new RecipeIngredient
        {
            IngredientId = ingredient.Id,
            UnitId = unit.Id,
            Quantity = quantity
        };
    }

    private static RecipeStep CreateRecipeStep(int stepNumber, string instruction)
    {
        return new RecipeStep
        {
            StepNumber = stepNumber,
            Instruction = instruction
        };
    }

    private static RecipeTool CreateRecipeTool(string name)
    {
        return new RecipeTool
        {
            Name = name
        };
    }

    private static bool NamesMatch(string first, string second)
    {
        return string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
    }
}
