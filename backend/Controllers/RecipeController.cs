using MealApp.Data;
using MealApp.DTOs;
using MealApp.Models;
using MealApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MealApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecipeController : ApiControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly RecipeReadService _reads;

    public RecipeController(ApplicationDbContext context, RecipeReadService reads)
    {
        _context = context;
        _reads = reads;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? categoryId, [FromQuery] string? search)
    {
        var query = _reads.Search(search, categoryId);

        var recipes = await query
            .Select(recipe => new RecipeResponseDto
            {
                Id = recipe.Id,
                Title = recipe.Title,
                Description = recipe.Description,
                ImageUrl = recipe.ImageUrl,
                PreparationTimeMinutes = recipe.PreparationTimeMinutes,
                CookingTimeMinutes = recipe.CookingTimeMinutes,
                Servings = recipe.Servings,
                CategoryId = recipe.CategoryId,
                CategoryName = recipe.Category != null ? recipe.Category.Name : null
            })
            .ToListAsync();

        return Ok(recipes);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var recipe = await _context.Recipes
            .Select(recipe => new RecipeResponseDto
            {
                Id = recipe.Id,
                Title = recipe.Title,
                Description = recipe.Description,
                ImageUrl = recipe.ImageUrl,
                PreparationTimeMinutes = recipe.PreparationTimeMinutes,
                CookingTimeMinutes = recipe.CookingTimeMinutes,
                Servings = recipe.Servings,
                CategoryId = recipe.CategoryId,
                CategoryName = recipe.Category != null ? recipe.Category.Name : null
            })
            .FirstOrDefaultAsync(recipe => recipe.Id == id);

        if (recipe is null)
        {
            return NotFound(new
            {
                Message = "Recipe not found."
            });
        }

        return Ok(recipe);
    }

    [HttpGet("{id}/details")]
    public async Task<IActionResult> GetDetails(int id)
    {
        var recipe = await _reads.GetDetailsAsync(id);

        if (recipe is null)
        {
            return NotFound(new
            {
                Message = "Recipe not found."
            });
        }

        return Ok(recipe);
    }

    [HttpGet("{id}/preview")]
    public async Task<IActionResult> GetPreview(int id, [FromQuery] int servings)
    {
        if (servings <= 0)
        {
            return BadRequest(new
            {
                Message = "Servings must be greater than zero."
            });
        }

        if (servings > 50)
        {
            return BadRequest(new
            {
                Message = "Servings cannot be greater than 50."
            });
        }

        var recipe = await _context.Recipes
            .Where(recipe => recipe.Id == id)
            .Select(recipe => new RecipeDetailsResponseDto
            {
                Id = recipe.Id,
                Title = recipe.Title,
                Description = recipe.Description,
                ImageUrl = recipe.ImageUrl,
                PreparationTimeMinutes = recipe.PreparationTimeMinutes,
                CookingTimeMinutes = recipe.CookingTimeMinutes,
                Servings = servings,
                CategoryId = recipe.CategoryId,
                CategoryName = recipe.Category != null ? recipe.Category.Name : null,
                Ingredients = recipe.RecipeIngredients
                    .OrderBy(recipeIngredient => recipeIngredient.Ingredient != null
                        ? recipeIngredient.Ingredient.Name
                        : null)
                    .Select(recipeIngredient => new RecipeDetailsIngredientResponseDto
                    {
                        IngredientId = recipeIngredient.IngredientId,
                        IngredientName = recipeIngredient.Ingredient != null ? recipeIngredient.Ingredient.Name : null,
                        Quantity = recipe.Servings > 0
                            ? recipeIngredient.Quantity * servings / recipe.Servings
                            : recipeIngredient.Quantity,
                        UnitId = recipeIngredient.UnitId,
                        UnitName = recipeIngredient.Unit != null ? recipeIngredient.Unit.Name : null,
                        UnitShortName = recipeIngredient.Unit != null ? recipeIngredient.Unit.ShortName : null
                    })
                    .ToList(),
                Steps = recipe.RecipeSteps
                    .OrderBy(step => step.StepNumber)
                    .Select(step => new RecipeStepResponseDto
                    {
                        Id = step.Id,
                        RecipeId = step.RecipeId,
                        StepNumber = step.StepNumber,
                        Instruction = step.Instruction
                    })
                    .ToList(),
                Tools = recipe.RecipeTools
                    .OrderBy(tool => tool.Name)
                    .Select(tool => new RecipeToolResponseDto
                    {
                        Id = tool.Id,
                        RecipeId = tool.RecipeId,
                        Name = tool.Name
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (recipe is null)
        {
            return NotFound(new
            {
                Message = "Recipe not found."
            });
        }

        return Ok(recipe);
    }

    [HttpGet("{id}/shopping-preview")]
    public async Task<IActionResult> GetShoppingPreview(int id, [FromQuery] int servings)
    {
        if (servings <= 0)
        {
            return BadRequest(new
            {
                Message = "Servings must be greater than zero."
            });
        }

        if (servings > 50)
        {
            return BadRequest(new
            {
                Message = "Servings cannot be greater than 50."
            });
        }

        var recipe = await _context.Recipes
            .Where(recipe => recipe.Id == id)
            .Select(recipe => new
            {
                Ingredients = recipe.RecipeIngredients
                    .OrderBy(recipeIngredient => recipeIngredient.Ingredient != null
                        ? recipeIngredient.Ingredient.Name
                        : null)
                    .Select(recipeIngredient => new RecipeShoppingPreviewResponseDto
                    {
                        IngredientName = recipeIngredient.Ingredient != null
                            ? recipeIngredient.Ingredient.Name
                            : null,
                        Quantity = recipe.Servings > 0
                            ? recipeIngredient.Quantity * servings / recipe.Servings
                            : recipeIngredient.Quantity,
                        UnitName = recipeIngredient.Unit != null
                            ? recipeIngredient.Unit.Name
                            : null,
                        UnitShortName = recipeIngredient.Unit != null
                            ? recipeIngredient.Unit.ShortName
                            : null
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (recipe is null)
        {
            return NotFound(new
            {
                Message = "Recipe not found."
            });
        }

        return Ok(recipe.Ingredients);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(CreateRecipeDto createRecipeDto)
    {
        var recipeTitleExists = await _context.Recipes
            .AnyAsync(recipe => recipe.Title.ToLower() == createRecipeDto.Title.ToLower());

        if (recipeTitleExists)
        {
            return BadRequest(new
            {
                Message = "Recipe title already exists."
            });
        }

        var category = await _context.Categories.FindAsync(createRecipeDto.CategoryId);

        if (category is null)
        {
            return BadRequest(new
            {
                Message = "Category does not exist."
            });
        }

        var recipe = new Recipe
        {
            Title = createRecipeDto.Title,
            Description = createRecipeDto.Description,
            ImageUrl = createRecipeDto.ImageUrl,
            PreparationTimeMinutes = createRecipeDto.PreparationTimeMinutes,
            CookingTimeMinutes = createRecipeDto.CookingTimeMinutes,
            Servings = createRecipeDto.Servings,
            CategoryId = createRecipeDto.CategoryId
        };

        _context.Recipes.Add(recipe);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = recipe.Id }, ToRecipeResponseDto(recipe, category.Name));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, UpdateRecipeDto updateRecipeDto)
    {
        var recipe = await _context.Recipes.FindAsync(id);

        if (recipe is null)
        {
            return NotFound(new
            {
                Message = "Recipe not found."
            });
        }

        var recipeTitleExists = await _context.Recipes
            .AnyAsync(recipe => recipe.Id != id
                && recipe.Title.ToLower() == updateRecipeDto.Title.ToLower());

        if (recipeTitleExists)
        {
            return BadRequest(new
            {
                Message = "Recipe title already exists."
            });
        }

        var category = await _context.Categories.FindAsync(updateRecipeDto.CategoryId);

        if (category is null)
        {
            return BadRequest(new
            {
                Message = "Category does not exist."
            });
        }

        recipe.Title = updateRecipeDto.Title;
        recipe.Description = updateRecipeDto.Description;
        recipe.ImageUrl = updateRecipeDto.ImageUrl;
        recipe.PreparationTimeMinutes = updateRecipeDto.PreparationTimeMinutes;
        recipe.CookingTimeMinutes = updateRecipeDto.CookingTimeMinutes;
        recipe.Servings = updateRecipeDto.Servings;
        recipe.CategoryId = updateRecipeDto.CategoryId;

        await _context.SaveChangesAsync();

        return Ok(ToRecipeResponseDto(recipe, category.Name));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var recipe = await _context.Recipes.FindAsync(id);

        if (recipe is null)
        {
            return NotFound(new
            {
                Message = "Recipe not found."
            });
        }

        _context.Recipes.Remove(recipe);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            Message = "Recipe deleted successfully."
        });
    }

    [HttpGet("{recipeId}/ingredients")]
    public async Task<IActionResult> GetIngredients(int recipeId)
    {
        var recipeExists = await _context.Recipes.AnyAsync(recipe => recipe.Id == recipeId);

        if (!recipeExists)
        {
            return NotFound(new
            {
                Message = "Recipe not found."
            });
        }

        var ingredients = await _context.RecipeIngredients
            .Where(recipeIngredient => recipeIngredient.RecipeId == recipeId)
            .Select(recipeIngredient => new RecipeIngredientResponseDto
            {
                Id = recipeIngredient.Id,
                RecipeId = recipeIngredient.RecipeId,
                IngredientId = recipeIngredient.IngredientId,
                IngredientName = recipeIngredient.Ingredient != null ? recipeIngredient.Ingredient.Name : null,
                Quantity = recipeIngredient.Quantity,
                UnitId = recipeIngredient.UnitId,
                UnitName = recipeIngredient.Unit != null ? recipeIngredient.Unit.Name : null,
                UnitShortName = recipeIngredient.Unit != null ? recipeIngredient.Unit.ShortName : null
            })
            .ToListAsync();

        return Ok(ingredients);
    }

    [HttpPost("{recipeId}/ingredients")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddIngredient(int recipeId, CreateRecipeIngredientDto createRecipeIngredientDto)
    {
        if (createRecipeIngredientDto.Quantity <= 0)
        {
            return BadRequest(new
            {
                Message = "Quantity must be greater than zero."
            });
        }

        var recipeIngredientExists = await _context.RecipeIngredients
            .AnyAsync(recipeIngredient => recipeIngredient.RecipeId == recipeId
                && recipeIngredient.IngredientId == createRecipeIngredientDto.IngredientId);

        if (recipeIngredientExists)
        {
            return BadRequest(new
            {
                Message = "Ingredient already exists in this recipe."
            });
        }

        var validationResult = await ValidateRecipeIngredientReferences(
            recipeId,
            createRecipeIngredientDto.IngredientId,
            createRecipeIngredientDto.UnitId);

        if (validationResult is not null)
        {
            return validationResult;
        }

        var recipeIngredient = new RecipeIngredient
        {
            RecipeId = recipeId,
            IngredientId = createRecipeIngredientDto.IngredientId,
            Quantity = createRecipeIngredientDto.Quantity,
            UnitId = createRecipeIngredientDto.UnitId
        };

        _context.RecipeIngredients.Add(recipeIngredient);
        await _context.SaveChangesAsync();

        return Ok(await GetRecipeIngredientResponseDto(recipeIngredient.Id));
    }

    [HttpPut("{recipeId}/ingredients/{recipeIngredientId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateIngredient(
        int recipeId,
        int recipeIngredientId,
        UpdateRecipeIngredientDto updateRecipeIngredientDto)
    {
        var recipeIngredient = await _context.RecipeIngredients
            .FirstOrDefaultAsync(item => item.Id == recipeIngredientId && item.RecipeId == recipeId);

        if (recipeIngredient is null)
        {
            return NotFound(new
            {
                Message = "Recipe ingredient not found."
            });
        }

        if (updateRecipeIngredientDto.Quantity <= 0)
        {
            return BadRequest(new
            {
                Message = "Quantity must be greater than zero."
            });
        }

        var recipeIngredientExists = await _context.RecipeIngredients
            .AnyAsync(item => item.RecipeId == recipeId
                && item.Id != recipeIngredientId
                && item.IngredientId == updateRecipeIngredientDto.IngredientId);

        if (recipeIngredientExists)
        {
            return BadRequest(new
            {
                Message = "Ingredient already exists in this recipe."
            });
        }

        var validationResult = await ValidateRecipeIngredientReferences(
            recipeId,
            updateRecipeIngredientDto.IngredientId,
            updateRecipeIngredientDto.UnitId);

        if (validationResult is not null)
        {
            return validationResult;
        }

        recipeIngredient.IngredientId = updateRecipeIngredientDto.IngredientId;
        recipeIngredient.Quantity = updateRecipeIngredientDto.Quantity;
        recipeIngredient.UnitId = updateRecipeIngredientDto.UnitId;

        await _context.SaveChangesAsync();

        return Ok(await GetRecipeIngredientResponseDto(recipeIngredient.Id));
    }

    [HttpDelete("{recipeId}/ingredients/{recipeIngredientId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteIngredient(int recipeId, int recipeIngredientId)
    {
        var recipeIngredient = await _context.RecipeIngredients
            .FirstOrDefaultAsync(item => item.Id == recipeIngredientId && item.RecipeId == recipeId);

        if (recipeIngredient is null)
        {
            return NotFound(new
            {
                Message = "Recipe ingredient not found."
            });
        }

        _context.RecipeIngredients.Remove(recipeIngredient);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            Message = "Recipe ingredient deleted successfully."
        });
    }

    [HttpGet("{recipeId}/steps")]
    public async Task<IActionResult> GetSteps(int recipeId)
    {
        var recipeExists = await _context.Recipes.AnyAsync(recipe => recipe.Id == recipeId);

        if (!recipeExists)
        {
            return NotFound(new
            {
                Message = "Recipe not found."
            });
        }

        var steps = await _context.RecipeSteps
            .Where(step => step.RecipeId == recipeId)
            .OrderBy(step => step.StepNumber)
            .Select(step => new RecipeStepResponseDto
            {
                Id = step.Id,
                RecipeId = step.RecipeId,
                StepNumber = step.StepNumber,
                Instruction = step.Instruction
            })
            .ToListAsync();

        return Ok(steps);
    }

    [HttpPost("{recipeId}/steps")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddStep(int recipeId, CreateRecipeStepDto createRecipeStepDto)
    {
        if (createRecipeStepDto.StepNumber <= 0)
        {
            return BadRequest(new
            {
                Message = "Step number must be greater than zero."
            });
        }

        var stepNumberExists = await _context.RecipeSteps
            .AnyAsync(step => step.RecipeId == recipeId
                && step.StepNumber == createRecipeStepDto.StepNumber);

        if (stepNumberExists)
        {
            return BadRequest(new
            {
                Message = "Step number already exists in this recipe."
            });
        }

        var recipeExists = await _context.Recipes.AnyAsync(recipe => recipe.Id == recipeId);

        if (!recipeExists)
        {
            return NotFound(new
            {
                Message = "Recipe not found."
            });
        }

        var step = new RecipeStep
        {
            RecipeId = recipeId,
            StepNumber = createRecipeStepDto.StepNumber,
            Instruction = createRecipeStepDto.Instruction
        };

        _context.RecipeSteps.Add(step);
        await _context.SaveChangesAsync();

        return Ok(ToRecipeStepResponseDto(step));
    }

    [HttpPut("{recipeId}/steps/{stepId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateStep(
        int recipeId,
        int stepId,
        UpdateRecipeStepDto updateRecipeStepDto)
    {
        var step = await _context.RecipeSteps
            .FirstOrDefaultAsync(item => item.Id == stepId && item.RecipeId == recipeId);

        if (step is null)
        {
            return NotFound(new
            {
                Message = "Recipe step not found."
            });
        }

        if (updateRecipeStepDto.StepNumber <= 0)
        {
            return BadRequest(new
            {
                Message = "Step number must be greater than zero."
            });
        }

        var stepNumberExists = await _context.RecipeSteps
            .AnyAsync(item => item.RecipeId == recipeId
                && item.Id != stepId
                && item.StepNumber == updateRecipeStepDto.StepNumber);

        if (stepNumberExists)
        {
            return BadRequest(new
            {
                Message = "Step number already exists in this recipe."
            });
        }

        step.StepNumber = updateRecipeStepDto.StepNumber;
        step.Instruction = updateRecipeStepDto.Instruction;

        await _context.SaveChangesAsync();

        return Ok(ToRecipeStepResponseDto(step));
    }

    [HttpDelete("{recipeId}/steps/{stepId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteStep(int recipeId, int stepId)
    {
        var step = await _context.RecipeSteps
            .FirstOrDefaultAsync(item => item.Id == stepId && item.RecipeId == recipeId);

        if (step is null)
        {
            return NotFound(new
            {
                Message = "Recipe step not found."
            });
        }

        _context.RecipeSteps.Remove(step);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            Message = "Recipe step deleted successfully."
        });
    }

    [HttpGet("{recipeId}/tools")]
    public async Task<IActionResult> GetTools(int recipeId)
    {
        var recipeExists = await _context.Recipes.AnyAsync(recipe => recipe.Id == recipeId);

        if (!recipeExists)
        {
            return NotFound(new
            {
                Message = "Recipe not found."
            });
        }

        var tools = await _context.RecipeTools
            .Where(tool => tool.RecipeId == recipeId)
            .Select(tool => new RecipeToolResponseDto
            {
                Id = tool.Id,
                RecipeId = tool.RecipeId,
                Name = tool.Name
            })
            .ToListAsync();

        return Ok(tools);
    }

    [HttpPost("{recipeId}/tools")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddTool(int recipeId, CreateRecipeToolDto createRecipeToolDto)
    {
        var toolNameExists = await _context.RecipeTools
            .AnyAsync(tool => tool.RecipeId == recipeId
                && tool.Name.ToLower() == createRecipeToolDto.Name.ToLower());

        if (toolNameExists)
        {
            return BadRequest(new
            {
                Message = "Tool already exists in this recipe."
            });
        }

        var recipeExists = await _context.Recipes.AnyAsync(recipe => recipe.Id == recipeId);

        if (!recipeExists)
        {
            return NotFound(new
            {
                Message = "Recipe not found."
            });
        }

        var tool = new RecipeTool
        {
            RecipeId = recipeId,
            Name = createRecipeToolDto.Name
        };

        _context.RecipeTools.Add(tool);
        await _context.SaveChangesAsync();

        return Ok(ToRecipeToolResponseDto(tool));
    }

    [HttpPut("{recipeId}/tools/{toolId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateTool(
        int recipeId,
        int toolId,
        UpdateRecipeToolDto updateRecipeToolDto)
    {
        var tool = await _context.RecipeTools
            .FirstOrDefaultAsync(item => item.Id == toolId && item.RecipeId == recipeId);

        if (tool is null)
        {
            return NotFound(new
            {
                Message = "Recipe tool not found."
            });
        }

        var toolNameExists = await _context.RecipeTools
            .AnyAsync(item => item.RecipeId == recipeId
                && item.Id != toolId
                && item.Name.ToLower() == updateRecipeToolDto.Name.ToLower());

        if (toolNameExists)
        {
            return BadRequest(new
            {
                Message = "Tool already exists in this recipe."
            });
        }

        tool.Name = updateRecipeToolDto.Name;

        await _context.SaveChangesAsync();

        return Ok(ToRecipeToolResponseDto(tool));
    }

    [HttpDelete("{recipeId}/tools/{toolId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteTool(int recipeId, int toolId)
    {
        var tool = await _context.RecipeTools
            .FirstOrDefaultAsync(item => item.Id == toolId && item.RecipeId == recipeId);

        if (tool is null)
        {
            return NotFound(new
            {
                Message = "Recipe tool not found."
            });
        }

        _context.RecipeTools.Remove(tool);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            Message = "Recipe tool deleted successfully."
        });
    }

    private async Task<IActionResult?> ValidateRecipeIngredientReferences(
        int recipeId,
        int ingredientId,
        int unitId)
    {
        var recipeExists = await _context.Recipes.AnyAsync(recipe => recipe.Id == recipeId);

        if (!recipeExists)
        {
            return NotFound(new
            {
                Message = "Recipe not found."
            });
        }

        var ingredientExists = await _context.Ingredients.AnyAsync(ingredient => ingredient.Id == ingredientId);

        if (!ingredientExists)
        {
            return BadRequest(new
            {
                Message = "Ingredient does not exist."
            });
        }

        var unitExists = await _context.Units.AnyAsync(unit => unit.Id == unitId);

        if (!unitExists)
        {
            return BadRequest(new
            {
                Message = "Unit does not exist."
            });
        }

        return null;
    }

    private static RecipeResponseDto ToRecipeResponseDto(Recipe recipe, string? categoryName)
    {
        return new RecipeResponseDto
        {
            Id = recipe.Id,
            Title = recipe.Title,
            Description = recipe.Description,
            ImageUrl = recipe.ImageUrl,
            PreparationTimeMinutes = recipe.PreparationTimeMinutes,
            CookingTimeMinutes = recipe.CookingTimeMinutes,
            Servings = recipe.Servings,
            CategoryId = recipe.CategoryId,
            CategoryName = categoryName
        };
    }

    private async Task<RecipeIngredientResponseDto?> GetRecipeIngredientResponseDto(int recipeIngredientId)
    {
        return await _context.RecipeIngredients
            .Where(recipeIngredient => recipeIngredient.Id == recipeIngredientId)
            .Select(recipeIngredient => new RecipeIngredientResponseDto
            {
                Id = recipeIngredient.Id,
                RecipeId = recipeIngredient.RecipeId,
                IngredientId = recipeIngredient.IngredientId,
                IngredientName = recipeIngredient.Ingredient != null ? recipeIngredient.Ingredient.Name : null,
                Quantity = recipeIngredient.Quantity,
                UnitId = recipeIngredient.UnitId,
                UnitName = recipeIngredient.Unit != null ? recipeIngredient.Unit.Name : null,
                UnitShortName = recipeIngredient.Unit != null ? recipeIngredient.Unit.ShortName : null
            })
            .FirstOrDefaultAsync();
    }

    private static RecipeStepResponseDto ToRecipeStepResponseDto(RecipeStep step)
    {
        return new RecipeStepResponseDto
        {
            Id = step.Id,
            RecipeId = step.RecipeId,
            StepNumber = step.StepNumber,
            Instruction = step.Instruction
        };
    }

    private static RecipeToolResponseDto ToRecipeToolResponseDto(RecipeTool tool)
    {
        return new RecipeToolResponseDto
        {
            Id = tool.Id,
            RecipeId = tool.RecipeId,
            Name = tool.Name
        };
    }
}
