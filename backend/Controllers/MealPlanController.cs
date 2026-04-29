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
public class MealPlanController : ApiControllerBase
{
    private readonly ApplicationDbContext _context;

    public MealPlanController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyMealPlans()
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var mealPlans = await _context.MealPlans
            .Where(mealPlan => mealPlan.UserId == userId)
            .OrderBy(mealPlan => mealPlan.PlannedDate)
            .Select(mealPlan => new MealPlanResponseDto
            {
                Id = mealPlan.Id,
                RecipeId = mealPlan.RecipeId,
                RecipeTitle = mealPlan.Recipe != null ? mealPlan.Recipe.Title : null,
                RecipeImageUrl = mealPlan.Recipe != null ? mealPlan.Recipe.ImageUrl : null,
                Servings = mealPlan.Servings,
                PlannedDate = mealPlan.PlannedDate,
                CreatedAt = mealPlan.CreatedAt
            })
            .ToListAsync();

        return Ok(mealPlans);
    }

    [HttpGet("my/range")]
    public async Task<IActionResult> GetMyMealPlansByRange([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        if (from > to)
        {
            return BadRequest(new
            {
                Message = "Invalid date range."
            });
        }

        var mealPlans = await _context.MealPlans
            .Where(mealPlan => mealPlan.UserId == userId
                && mealPlan.PlannedDate >= from
                && mealPlan.PlannedDate <= to)
            .OrderBy(mealPlan => mealPlan.PlannedDate)
            .Select(mealPlan => new MealPlanResponseDto
            {
                Id = mealPlan.Id,
                RecipeId = mealPlan.RecipeId,
                RecipeTitle = mealPlan.Recipe != null ? mealPlan.Recipe.Title : null,
                RecipeImageUrl = mealPlan.Recipe != null ? mealPlan.Recipe.ImageUrl : null,
                Servings = mealPlan.Servings,
                PlannedDate = mealPlan.PlannedDate,
                CreatedAt = mealPlan.CreatedAt
            })
            .ToListAsync();

        return Ok(mealPlans);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateMealPlanDto createMealPlanDto)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var recipeExists = await _context.Recipes.AnyAsync(recipe => recipe.Id == createMealPlanDto.RecipeId);

        if (!recipeExists)
        {
            return BadRequest(new
            {
                Message = "Recipe does not exist."
            });
        }

        if (createMealPlanDto.Servings <= 0)
        {
            return BadRequest(new
            {
                Message = "Servings must be greater than zero."
            });
        }

        if (createMealPlanDto.Servings > 50)
        {
            return BadRequest(new
            {
                Message = "Servings cannot be greater than 50."
            });
        }

        var mealPlanExists = await _context.MealPlans
            .AnyAsync(mealPlan => mealPlan.UserId == userId
                && mealPlan.RecipeId == createMealPlanDto.RecipeId
                && mealPlan.PlannedDate == createMealPlanDto.PlannedDate);

        if (mealPlanExists)
        {
            return BadRequest(new
            {
                Message = "This recipe is already planned for this date."
            });
        }

        var mealPlan = new MealPlan
        {
            UserId = userId,
            RecipeId = createMealPlanDto.RecipeId,
            Servings = createMealPlanDto.Servings,
            PlannedDate = createMealPlanDto.PlannedDate,
            CreatedAt = DateTime.UtcNow
        };

        _context.MealPlans.Add(mealPlan);
        await _context.SaveChangesAsync();

        return Ok(await GetMealPlanResponseDto(mealPlan.Id, userId));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateMealPlanDto updateMealPlanDto)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var mealPlan = await _context.MealPlans
            .FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId);

        if (mealPlan is null)
        {
            return NotFound(new
            {
                Message = "Meal plan not found."
            });
        }

        var recipeExists = await _context.Recipes.AnyAsync(recipe => recipe.Id == updateMealPlanDto.RecipeId);

        if (!recipeExists)
        {
            return BadRequest(new
            {
                Message = "Recipe does not exist."
            });
        }

        if (updateMealPlanDto.Servings <= 0)
        {
            return BadRequest(new
            {
                Message = "Servings must be greater than zero."
            });
        }

        if (updateMealPlanDto.Servings > 50)
        {
            return BadRequest(new
            {
                Message = "Servings cannot be greater than 50."
            });
        }

        var mealPlanExists = await _context.MealPlans
            .AnyAsync(item => item.Id != id
                && item.UserId == userId
                && item.RecipeId == updateMealPlanDto.RecipeId
                && item.PlannedDate == updateMealPlanDto.PlannedDate);

        if (mealPlanExists)
        {
            return BadRequest(new
            {
                Message = "This recipe is already planned for this date."
            });
        }

        mealPlan.RecipeId = updateMealPlanDto.RecipeId;
        mealPlan.Servings = updateMealPlanDto.Servings;
        mealPlan.PlannedDate = updateMealPlanDto.PlannedDate;

        await _context.SaveChangesAsync();

        return Ok(await GetMealPlanResponseDto(mealPlan.Id, userId));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var mealPlan = await _context.MealPlans
            .FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId);

        if (mealPlan is null)
        {
            return NotFound(new
            {
                Message = "Meal plan not found."
            });
        }

        _context.MealPlans.Remove(mealPlan);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            Message = "Meal plan deleted successfully."
        });
    }

    [HttpDelete("my/range")]
    public async Task<IActionResult> DeleteMyMealPlansByRange([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        if (from > to)
        {
            return BadRequest(new
            {
                Message = "Invalid date range."
            });
        }

        var mealPlans = await _context.MealPlans
            .Where(mealPlan => mealPlan.UserId == userId
                && mealPlan.PlannedDate >= from
                && mealPlan.PlannedDate <= to)
            .ToListAsync();

        if (mealPlans.Count == 0)
        {
            return NotFound(new
            {
                Message = "No meal plans found in this date range."
            });
        }

        _context.MealPlans.RemoveRange(mealPlans);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            Message = "Meal plans deleted successfully."
        });
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    private async Task<MealPlanResponseDto?> GetMealPlanResponseDto(int id, string userId)
    {
        return await _context.MealPlans
            .Where(mealPlan => mealPlan.Id == id && mealPlan.UserId == userId)
            .Select(mealPlan => new MealPlanResponseDto
            {
                Id = mealPlan.Id,
                RecipeId = mealPlan.RecipeId,
                RecipeTitle = mealPlan.Recipe != null ? mealPlan.Recipe.Title : null,
                RecipeImageUrl = mealPlan.Recipe != null ? mealPlan.Recipe.ImageUrl : null,
                Servings = mealPlan.Servings,
                PlannedDate = mealPlan.PlannedDate,
                CreatedAt = mealPlan.CreatedAt
            })
            .FirstOrDefaultAsync();
    }
}
