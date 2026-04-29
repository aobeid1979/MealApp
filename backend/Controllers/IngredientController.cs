using MealApp.Data;
using MealApp.DTOs;
using MealApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MealApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IngredientController : ApiControllerBase
{
    private readonly ApplicationDbContext _context;

    public IngredientController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var ingredients = await _context.Ingredients
            .Include(ingredient => ingredient.IngredientCategory)
            .Select(ingredient => new IngredientResponseDto
            {
                Id = ingredient.Id,
                Name = ingredient.Name,
                Description = ingredient.Description,
                ImageUrl = ingredient.ImageUrl,
                IngredientCategoryId = ingredient.IngredientCategoryId,
                IngredientCategoryName = ingredient.IngredientCategory != null
                    ? ingredient.IngredientCategory.Name
                    : null
            })
            .ToListAsync();

        return Ok(ingredients);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var ingredient = await _context.Ingredients
            .Include(ingredient => ingredient.IngredientCategory)
            .FirstOrDefaultAsync(ingredient => ingredient.Id == id);

        if (ingredient is null)
        {
            return NotFound(new
            {
                Message = "Ingredient not found."
            });
        }

        return Ok(ToResponseDto(ingredient));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(CreateIngredientDto createIngredientDto)
    {
        var ingredientCategoryExists = await _context.IngredientCategories
            .AnyAsync(ingredientCategory => ingredientCategory.Id == createIngredientDto.IngredientCategoryId);

        if (!ingredientCategoryExists)
        {
            return BadRequest(new
            {
                Message = "Ingredient category does not exist."
            });
        }

        var ingredient = new Ingredient
        {
            Name = createIngredientDto.Name,
            Description = createIngredientDto.Description,
            ImageUrl = createIngredientDto.ImageUrl,
            IngredientCategoryId = createIngredientDto.IngredientCategoryId
        };

        _context.Ingredients.Add(ingredient);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = ingredient.Id }, ToResponseDto(ingredient));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, UpdateIngredientDto updateIngredientDto)
    {
        var ingredient = await _context.Ingredients.FindAsync(id);

        if (ingredient is null)
        {
            return NotFound(new
            {
                Message = "Ingredient not found."
            });
        }

        var ingredientCategoryExists = await _context.IngredientCategories
            .AnyAsync(ingredientCategory => ingredientCategory.Id == updateIngredientDto.IngredientCategoryId);

        if (!ingredientCategoryExists)
        {
            return BadRequest(new
            {
                Message = "Ingredient category does not exist."
            });
        }

        ingredient.Name = updateIngredientDto.Name;
        ingredient.Description = updateIngredientDto.Description;
        ingredient.ImageUrl = updateIngredientDto.ImageUrl;
        ingredient.IngredientCategoryId = updateIngredientDto.IngredientCategoryId;

        await _context.SaveChangesAsync();

        return Ok(ToResponseDto(ingredient));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var ingredient = await _context.Ingredients.FindAsync(id);

        if (ingredient is null)
        {
            return NotFound(new
            {
                Message = "Ingredient not found."
            });
        }

        _context.Ingredients.Remove(ingredient);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            Message = "Ingredient deleted successfully."
        });
    }

    private static IngredientResponseDto ToResponseDto(Ingredient ingredient)
    {
        return new IngredientResponseDto
        {
            Id = ingredient.Id,
            Name = ingredient.Name,
            Description = ingredient.Description,
            ImageUrl = ingredient.ImageUrl,
            IngredientCategoryId = ingredient.IngredientCategoryId,
            IngredientCategoryName = ingredient.IngredientCategory?.Name
        };
    }
}
