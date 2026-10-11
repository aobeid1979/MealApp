using MealApp.Data;
using MealApp.DTOs;
using MealApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MealApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoryController : ApiControllerBase
{
    private readonly ApplicationDbContext _context;

    public CategoryController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var categories = await _context.Categories
            .Select(category => new CategoryResponseDto
            {
                Id = category.Id,
                Name = category.Name,
                Description = category.Description
            })
            .ToListAsync();

        return Ok(categories);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var category = await _context.Categories.FindAsync(id);

        if (category is null)
        {
            return NotFound(new
            {
                Message = "Category not found."
            });
        }

        return Ok(ToResponseDto(category));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(CreateCategoryDto createCategoryDto)
    {
        var categoryExists = await _context.Categories
            .AnyAsync(category => category.Name.ToLower() == createCategoryDto.Name.ToLower());

        if (categoryExists)
        {
            return BadRequest(new
            {
                Message = "Category name already exists."
            });
        }

        var category = new Category
        {
            Name = createCategoryDto.Name,
            Description = createCategoryDto.Description
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = category.Id }, ToResponseDto(category));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, UpdateCategoryDto updateCategoryDto)
    {
        var category = await _context.Categories.FindAsync(id);

        if (category is null)
        {
            return NotFound(new
            {
                Message = "Category not found."
            });
        }

        var categoryNameExists = await _context.Categories
            .AnyAsync(category => category.Id != id
                && category.Name.ToLower() == updateCategoryDto.Name.ToLower());

        if (categoryNameExists)
        {
            return BadRequest(new
            {
                Message = "Category name already exists."
            });
        }

        category.Name = updateCategoryDto.Name;
        category.Description = updateCategoryDto.Description;

        await _context.SaveChangesAsync();

        return Ok(ToResponseDto(category));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _context.Categories.FindAsync(id);

        if (category is null)
        {
            return NotFound(new
            {
                Message = "Category not found."
            });
        }

        var categoryHasRecipes = await _context.Recipes
            .CountAsync(recipe => recipe.CategoryId == id);

        if (categoryHasRecipes > 0)
        {
            return StatusCode(
                StatusCodes.Status409Conflict,
                new ApiResponse
                {
                    IsSuccess = false,
                    Result = new
                    {
                        RecipeCount = categoryHasRecipes
                    },
                    ErrorMessages =
                    [
                        "Cannot delete category because it is used by existing recipes."
                    ],
                    StatusCode = StatusCodes.Status409Conflict
                });
        }

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            Message = "Category deleted successfully."
        });
    }

    private static CategoryResponseDto ToResponseDto(Category category)
    {
        return new CategoryResponseDto
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description
        };
    }
}
