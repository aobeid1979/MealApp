using MealApp.Data;
using MealApp.DTOs;
using MealApp.Models;
using Microsoft.EntityFrameworkCore;

namespace MealApp.Services;

public class RecipeReadService(ApplicationDbContext context)
{
    public IQueryable<Recipe> Search(string? search = null, int? categoryId = null, string? category = null)
    {
        var query = context.Recipes.AsNoTracking();
        if (categoryId.HasValue) query = query.Where(x => x.CategoryId == categoryId.Value);
        if (!string.IsNullOrWhiteSpace(category))
        {
            var name = category.ToLowerInvariant();
            query = query.Where(x => x.Category != null && x.Category.Name.ToLower() == name);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.ToLowerInvariant();
            query = query.Where(x => x.Title.ToLower().Contains(text)
                || (x.Description != null && x.Description.ToLower().Contains(text)));
        }
        return query;
    }

    public IQueryable<RecipeDetailsIngredientResponseDto> Ingredients(int recipeId)
        => context.RecipeIngredients.AsNoTracking().Where(x => x.RecipeId == recipeId)
            .OrderBy(x => x.Ingredient != null ? x.Ingredient.Name : null).ThenBy(x => x.Id)
            .Select(x => new RecipeDetailsIngredientResponseDto
            {
                IngredientId = x.IngredientId, IngredientName = x.Ingredient != null ? x.Ingredient.Name : null,
                Quantity = x.Quantity, UnitId = x.UnitId,
                UnitName = x.Unit != null ? x.Unit.Name : null, UnitShortName = x.Unit != null ? x.Unit.ShortName : null
            });

    public Task<RecipeDetailsResponseDto?> GetDetailsAsync(int id, CancellationToken ct = default)
        => context.Recipes.AsNoTracking().Where(x => x.Id == id)
            .Select(recipe => new RecipeDetailsResponseDto
            {
                Id = recipe.Id, Title = recipe.Title, Description = recipe.Description, ImageUrl = recipe.ImageUrl,
                PreparationTimeMinutes = recipe.PreparationTimeMinutes, CookingTimeMinutes = recipe.CookingTimeMinutes,
                Servings = recipe.Servings, CategoryId = recipe.CategoryId,
                CategoryName = recipe.Category != null ? recipe.Category.Name : null,
                Ingredients = recipe.RecipeIngredients
                    .OrderBy(x => x.Ingredient != null ? x.Ingredient.Name : null).ThenBy(x => x.Id)
                    .Select(x => new RecipeDetailsIngredientResponseDto
                    {
                        IngredientId = x.IngredientId, IngredientName = x.Ingredient != null ? x.Ingredient.Name : null,
                        Quantity = x.Quantity, UnitId = x.UnitId,
                        UnitName = x.Unit != null ? x.Unit.Name : null, UnitShortName = x.Unit != null ? x.Unit.ShortName : null
                    }).ToList(),
                Steps = recipe.RecipeSteps.OrderBy(x => x.StepNumber).ThenBy(x => x.Id)
                    .Select(x => new RecipeStepResponseDto { Id = x.Id, RecipeId = x.RecipeId, StepNumber = x.StepNumber, Instruction = x.Instruction }).ToList(),
                Tools = recipe.RecipeTools.OrderBy(x => x.Name).ThenBy(x => x.Id)
                    .Select(x => new RecipeToolResponseDto { Id = x.Id, RecipeId = x.RecipeId, Name = x.Name }).ToList()
            }).FirstOrDefaultAsync(ct);
}
