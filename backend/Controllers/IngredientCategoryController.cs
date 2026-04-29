using MealApp.Data;
using MealApp.DTOs;
using MealApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MealApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IngredientCategoryController : ApiControllerBase
{
    private readonly ApplicationDbContext _context;

    public IngredientCategoryController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var ingredientCategories = await _context.IngredientCategories
            .Select(ingredientCategory => new IngredientCategoryResponseDto
            {
                Id = ingredientCategory.Id,
                Name = ingredientCategory.Name,
                Description = ingredientCategory.Description
            })
            .ToListAsync();

        return Ok(ingredientCategories);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var ingredientCategory = await _context.IngredientCategories.FindAsync(id);

        if (ingredientCategory is null)
        {
            return NotFound(new
            {
                Message = "Ingredient category not found."
            });
        }

        return Ok(ToResponseDto(ingredientCategory));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(CreateIngredientCategoryDto createIngredientCategoryDto)
    {
        var ingredientCategoryExists = await _context.IngredientCategories
            .AnyAsync(ingredientCategory =>
                ingredientCategory.Name.ToLower() == createIngredientCategoryDto.Name.ToLower());

        if (ingredientCategoryExists)
        {
            return BadRequest(new
            {
                Message = "Ingredient category name already exists."
            });
        }

        var ingredientCategory = new IngredientCategory
        {
            Name = createIngredientCategoryDto.Name,
            Description = createIngredientCategoryDto.Description
        };

        _context.IngredientCategories.Add(ingredientCategory);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = ingredientCategory.Id }, ToResponseDto(ingredientCategory));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, UpdateIngredientCategoryDto updateIngredientCategoryDto)
    {
        var ingredientCategory = await _context.IngredientCategories.FindAsync(id);

        if (ingredientCategory is null)
        {
            return NotFound(new
            {
                Message = "Ingredient category not found."
            });
        }

        var ingredientCategoryNameExists = await _context.IngredientCategories
            .AnyAsync(existingIngredientCategory => existingIngredientCategory.Id != id
                && existingIngredientCategory.Name.ToLower() == updateIngredientCategoryDto.Name.ToLower());

        if (ingredientCategoryNameExists)
        {
            return BadRequest(new
            {
                Message = "Ingredient category name already exists."
            });
        }

        ingredientCategory.Name = updateIngredientCategoryDto.Name;
        ingredientCategory.Description = updateIngredientCategoryDto.Description;

        await _context.SaveChangesAsync();

        return Ok(ToResponseDto(ingredientCategory));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var ingredientCategory = await _context.IngredientCategories.FindAsync(id);

        if (ingredientCategory is null)
        {
            return NotFound(new
            {
                Message = "Ingredient category not found."
            });
        }

        _context.IngredientCategories.Remove(ingredientCategory);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            Message = "Ingredient category deleted successfully."
        });
    }

    private static IngredientCategoryResponseDto ToResponseDto(IngredientCategory ingredientCategory)
    {
        return new IngredientCategoryResponseDto
        {
            Id = ingredientCategory.Id,
            Name = ingredientCategory.Name,
            Description = ingredientCategory.Description
        };
    }
}
