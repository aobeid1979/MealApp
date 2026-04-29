using MealApp.Data;
using MealApp.DTOs;
using MealApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MealApp.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class FavoriteRecipeController : ApiControllerBase
{
    private readonly ApplicationDbContext _context;

    public FavoriteRecipeController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyFavorites()
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var favorites = await _context.FavoriteRecipes
            .Where(favoriteRecipe => favoriteRecipe.UserId == userId)
            .OrderByDescending(favoriteRecipe => favoriteRecipe.CreatedAt)
            .Select(favoriteRecipe => new FavoriteRecipeResponseDto
            {
                Id = favoriteRecipe.Id,
                RecipeId = favoriteRecipe.RecipeId,
                RecipeTitle = favoriteRecipe.Recipe != null ? favoriteRecipe.Recipe.Title : null,
                RecipeDescription = favoriteRecipe.Recipe != null ? favoriteRecipe.Recipe.Description : null,
                RecipeImageUrl = favoriteRecipe.Recipe != null ? favoriteRecipe.Recipe.ImageUrl : null,
                PreparationTimeMinutes = favoriteRecipe.Recipe != null
                    ? favoriteRecipe.Recipe.PreparationTimeMinutes
                    : 0,
                CookingTimeMinutes = favoriteRecipe.Recipe != null ? favoriteRecipe.Recipe.CookingTimeMinutes : 0,
                Servings = favoriteRecipe.Recipe != null ? favoriteRecipe.Recipe.Servings : 0,
                CategoryId = favoriteRecipe.Recipe != null ? favoriteRecipe.Recipe.CategoryId : 0,
                CategoryName = favoriteRecipe.Recipe != null && favoriteRecipe.Recipe.Category != null
                    ? favoriteRecipe.Recipe.Category.Name
                    : null,
                CreatedAt = favoriteRecipe.CreatedAt
            })
            .ToListAsync();

        return Ok(favorites);
    }

    [HttpPost("{recipeId}")]
    public async Task<IActionResult> AddToFavorites(int recipeId)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var recipeExists = await _context.Recipes.AnyAsync(recipe => recipe.Id == recipeId);

        if (!recipeExists)
        {
            return BadRequest(new
            {
                Message = "Recipe does not exist."
            });
        }

        var favoriteExists = await _context.FavoriteRecipes
            .AnyAsync(favoriteRecipe => favoriteRecipe.UserId == userId
                && favoriteRecipe.RecipeId == recipeId);

        if (favoriteExists)
        {
            return BadRequest(new
            {
                Message = "Recipe is already in favorites."
            });
        }

        var favoriteRecipe = new FavoriteRecipe
        {
            UserId = userId,
            RecipeId = recipeId,
            CreatedAt = DateTime.UtcNow
        };

        _context.FavoriteRecipes.Add(favoriteRecipe);
        await _context.SaveChangesAsync();

        return Ok(await GetFavoriteRecipeResponseDto(favoriteRecipe.Id, userId));
    }

    [HttpDelete("{recipeId}")]
    public async Task<IActionResult> RemoveFromFavorites(int recipeId)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var favoriteRecipe = await _context.FavoriteRecipes
            .FirstOrDefaultAsync(favoriteRecipe => favoriteRecipe.UserId == userId
                && favoriteRecipe.RecipeId == recipeId);

        if (favoriteRecipe is null)
        {
            return NotFound(new
            {
                Message = "Favorite recipe not found."
            });
        }

        _context.FavoriteRecipes.Remove(favoriteRecipe);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            Message = "Recipe removed from favorites."
        });
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    private async Task<FavoriteRecipeResponseDto?> GetFavoriteRecipeResponseDto(int id, string userId)
    {
        return await _context.FavoriteRecipes
            .Where(favoriteRecipe => favoriteRecipe.Id == id && favoriteRecipe.UserId == userId)
            .Select(favoriteRecipe => new FavoriteRecipeResponseDto
            {
                Id = favoriteRecipe.Id,
                RecipeId = favoriteRecipe.RecipeId,
                RecipeTitle = favoriteRecipe.Recipe != null ? favoriteRecipe.Recipe.Title : null,
                RecipeDescription = favoriteRecipe.Recipe != null ? favoriteRecipe.Recipe.Description : null,
                RecipeImageUrl = favoriteRecipe.Recipe != null ? favoriteRecipe.Recipe.ImageUrl : null,
                PreparationTimeMinutes = favoriteRecipe.Recipe != null
                    ? favoriteRecipe.Recipe.PreparationTimeMinutes
                    : 0,
                CookingTimeMinutes = favoriteRecipe.Recipe != null ? favoriteRecipe.Recipe.CookingTimeMinutes : 0,
                Servings = favoriteRecipe.Recipe != null ? favoriteRecipe.Recipe.Servings : 0,
                CategoryId = favoriteRecipe.Recipe != null ? favoriteRecipe.Recipe.CategoryId : 0,
                CategoryName = favoriteRecipe.Recipe != null && favoriteRecipe.Recipe.Category != null
                    ? favoriteRecipe.Recipe.Category.Name
                    : null,
                CreatedAt = favoriteRecipe.CreatedAt
            })
            .FirstOrDefaultAsync();
    }
}
