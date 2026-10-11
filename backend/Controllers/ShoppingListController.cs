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
public class ShoppingListController : ApiControllerBase
{
    private readonly ApplicationDbContext _context;

    public ShoppingListController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userId is null)
        {
            return Unauthorized();
        }

        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            return BadRequest(new
            {
                Message = "Invalid date range."
            });
        }

        var mealPlanQuery = _context.MealPlans
            .Where(mealPlan => mealPlan.UserId == userId);

        if (from.HasValue && to.HasValue)
        {
            mealPlanQuery = mealPlanQuery
                .Where(mealPlan => mealPlan.PlannedDate >= from.Value
                    && mealPlan.PlannedDate <= to.Value);
        }

        var shoppingList = await GetOrCreateShoppingList(userId);

        var mealPlans = await mealPlanQuery
            .Include(mealPlan => mealPlan.Recipe)
                .ThenInclude(recipe => recipe!.RecipeIngredients)
            .ToListAsync();

        if (mealPlans.Count == 0)
        {
            await SyncGeneratedShoppingListItems(shoppingList.Id, userId, []);
            await _context.SaveChangesAsync();

            return Ok(await GetShoppingListResponseDto(shoppingList.Id, userId));
        }

        var mergedItems = BuildMergedShoppingListItems(mealPlans);

        if (mergedItems.Count == 0)
        {
            await SyncGeneratedShoppingListItems(shoppingList.Id, userId, []);
            await _context.SaveChangesAsync();

            return Ok(await GetShoppingListResponseDto(shoppingList.Id, userId));
        }

        await SyncGeneratedShoppingListItems(shoppingList.Id, userId, mergedItems);
        await _context.SaveChangesAsync();

        var response = await GetShoppingListResponseDto(shoppingList.Id, userId);

        return Ok(response);
    }

    [HttpPost("generate-selected")]
    public async Task<IActionResult> GenerateSelected(GenerateSelectedShoppingListDto generateSelectedShoppingListDto)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        if (generateSelectedShoppingListDto.MealPlanIds is null
            || generateSelectedShoppingListDto.MealPlanIds.Count == 0)
        {
            return BadRequest(new
            {
                Message = "Meal plan ids are required."
            });
        }

        var mealPlanIds = generateSelectedShoppingListDto.MealPlanIds
            .Distinct()
            .ToList();

        var shoppingListName = generateSelectedShoppingListDto.Name?.Trim();

        if (shoppingListName is not null && shoppingListName.Length > 100)
        {
            return BadRequest(new
            {
                Message = "Shopping list name cannot be longer than 100 characters."
            });
        }

        var mealPlans = await _context.MealPlans
            .Where(mealPlan => mealPlan.UserId == userId
                && mealPlanIds.Contains(mealPlan.Id))
            .Include(mealPlan => mealPlan.Recipe)
                .ThenInclude(recipe => recipe!.RecipeIngredients)
            .ToListAsync();

        if (mealPlans.Count == 0)
        {
            return BadRequest(new
            {
                Message = "No valid meal plans found."
            });
        }

        var mergedItems = BuildMergedShoppingListItems(mealPlans);

        var shoppingList = await GetOrCreateShoppingList(
            userId,
            !string.IsNullOrWhiteSpace(shoppingListName)
                ? shoppingListName
                : null);

        if (mergedItems.Count == 0)
        {
            await SyncGeneratedShoppingListItems(shoppingList.Id, userId, []);
            await _context.SaveChangesAsync();

            return Ok(await GetShoppingListResponseDto(shoppingList.Id, userId));
        }

        await SyncGeneratedShoppingListItems(shoppingList.Id, userId, mergedItems);
        await _context.SaveChangesAsync();

        var response = await GetShoppingListResponseDto(shoppingList.Id, userId);

        return Ok(response);
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyShoppingLists()
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var activeShoppingListId = await GetActiveShoppingListId(userId);

        var shoppingLists = await _context.ShoppingLists
            .Where(shoppingList => shoppingList.Id == activeShoppingListId
                && shoppingList.UserId == userId)
            .Select(shoppingList => new ShoppingListResponseDto
            {
                Id = shoppingList.Id,
                Name = shoppingList.Name,
                CreatedAt = shoppingList.CreatedAt,
                Items = shoppingList.Items
                    .OrderBy(item => item.Ingredient != null
                        && item.Ingredient.IngredientCategory != null
                            ? item.Ingredient.IngredientCategory.Name
                            : null)
                    .ThenBy(item => item.Ingredient != null
                        ? item.Ingredient.Name
                        : item.CustomName)
                    .Select(item => new ShoppingListItemResponseDto
                    {
                        Id = item.Id,
                        IngredientId = item.IngredientId,
                        IngredientName = item.Ingredient != null ? item.Ingredient.Name : null,
                        CustomName = item.CustomName,
                        ItemName = item.Ingredient != null ? item.Ingredient.Name : item.CustomName,
                        IngredientCategoryId = item.Ingredient != null
                            && item.Ingredient.IngredientCategory != null
                                ? item.Ingredient.IngredientCategory.Id
                                : null,
                        IngredientCategoryName = item.Ingredient != null
                            && item.Ingredient.IngredientCategory != null
                                ? item.Ingredient.IngredientCategory.Name
                                : null,
                        Quantity = item.Quantity,
                        UnitId = item.UnitId,
                        UnitName = item.Unit != null ? item.Unit.Name : null,
                        UnitShortName = item.Unit != null ? item.Unit.ShortName : null,
                        IsChecked = item.IsChecked,
                        IsGenerated = item.IsGenerated == true
                    })
                    .ToList()
            })
            .ToListAsync();

        return Ok(shoppingLists);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var shoppingList = await GetShoppingListResponseDto(id, userId);

        if (shoppingList is null)
        {
            return NotFound(new
            {
                Message = "Shopping list not found."
            });
        }

        return Ok(shoppingList);
    }

    [HttpGet("{id}/grouped")]
    public async Task<IActionResult> GetGrouped(int id)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var shoppingListExists = await _context.ShoppingLists
            .AnyAsync(shoppingList => shoppingList.Id == id && shoppingList.UserId == userId);

        if (!shoppingListExists)
        {
            return NotFound(new
            {
                Message = "Shopping list not found."
            });
        }

        var items = await _context.ShoppingListItems
            .Where(item => item.ShoppingListId == id
                && item.ShoppingList != null
                && item.ShoppingList.UserId == userId)
            .Select(item => new ShoppingListItemResponseDto
            {
                Id = item.Id,
                IngredientId = item.IngredientId,
                IngredientName = item.Ingredient != null ? item.Ingredient.Name : null,
                CustomName = item.CustomName,
                ItemName = item.Ingredient != null ? item.Ingredient.Name : item.CustomName,
                IngredientCategoryId = item.Ingredient != null
                    && item.Ingredient.IngredientCategory != null
                        ? item.Ingredient.IngredientCategory.Id
                        : null,
                IngredientCategoryName = item.Ingredient != null
                    && item.Ingredient.IngredientCategory != null
                        ? item.Ingredient.IngredientCategory.Name
                        : null,
                Quantity = item.Quantity,
                UnitId = item.UnitId,
                UnitName = item.Unit != null ? item.Unit.Name : null,
                UnitShortName = item.Unit != null ? item.Unit.ShortName : null,
                IsChecked = item.IsChecked,
                IsGenerated = item.IsGenerated == true
            })
            .ToListAsync();

        var groupedItems = items
            .GroupBy(item => new
            {
                CategoryId = item.IngredientCategoryId,
                CategoryName = item.IngredientCategoryName
            })
            .OrderBy(group => group.Key.CategoryName)
            .Select(group => new ShoppingListGroupedResponseDto
            {
                CategoryId = group.Key.CategoryId,
                CategoryName = group.Key.CategoryName,
                Items = group
                    .OrderBy(item => item.IngredientName ?? item.CustomName)
                    .ToList()
            })
            .ToList();

        return Ok(groupedItems);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Rename(int id, UpdateShoppingListDto updateShoppingListDto)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var shoppingList = await _context.ShoppingLists
            .FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId);

        if (shoppingList is null)
        {
            return NotFound(new
            {
                Message = "Shopping list not found."
            });
        }

        shoppingList.Name = updateShoppingListDto.Name;
        await _context.SaveChangesAsync();

        return Ok(await GetShoppingListResponseDto(shoppingList.Id, userId));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var shoppingList = await _context.ShoppingLists
            .FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId);

        if (shoppingList is null)
        {
            return NotFound(new
            {
                Message = "Shopping list not found."
            });
        }

        _context.ShoppingLists.Remove(shoppingList);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            Message = "Shopping list deleted successfully."
        });
    }

    [HttpDelete("{id}/checked-items")]
    public async Task<IActionResult> ClearCheckedItems(int id)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var shoppingListExists = await _context.ShoppingLists
            .AnyAsync(shoppingList => shoppingList.Id == id && shoppingList.UserId == userId);

        if (!shoppingListExists)
        {
            return NotFound(new
            {
                Message = "Shopping list not found."
            });
        }

        var checkedItems = await _context.ShoppingListItems
            .Where(item => item.ShoppingListId == id
                && item.ShoppingList != null
                && item.ShoppingList.UserId == userId
                && item.IsChecked)
            .ToListAsync();

        _context.ShoppingListItems.RemoveRange(checkedItems);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            Message = "Checked shopping list items cleared successfully."
        });
    }

    [HttpDelete("{id}/items")]
    public async Task<IActionResult> ClearItems(int id)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var shoppingListExists = await _context.ShoppingLists
            .AnyAsync(shoppingList => shoppingList.Id == id && shoppingList.UserId == userId);

        if (!shoppingListExists)
        {
            return NotFound(new
            {
                Message = "Shopping list not found."
            });
        }

        var items = await _context.ShoppingListItems
            .Where(item => item.ShoppingListId == id
                && item.ShoppingList != null
                && item.ShoppingList.UserId == userId)
            .ToListAsync();

        _context.ShoppingListItems.RemoveRange(items);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            Message = "Shopping list cleared successfully."
        });
    }

    [HttpPatch("{id}/items/check-state")]
    public async Task<IActionResult> UpdateItemsCheckState(
        int id,
        UpdateShoppingListItemsCheckStateDto updateShoppingListItemsCheckStateDto)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var shoppingListExists = await _context.ShoppingLists
            .AnyAsync(shoppingList => shoppingList.Id == id && shoppingList.UserId == userId);

        if (!shoppingListExists)
        {
            return NotFound(new
            {
                Message = "Shopping list not found."
            });
        }

        var items = await _context.ShoppingListItems
            .Where(item => item.ShoppingListId == id
                && item.ShoppingList != null
                && item.ShoppingList.UserId == userId)
            .ToListAsync();

        foreach (var item in items)
        {
            item.IsChecked = updateShoppingListItemsCheckStateDto.IsChecked;
        }

        await _context.SaveChangesAsync();

        return Ok(await GetShoppingListResponseDto(id, userId));
    }

    [HttpPost("{id}/items")]
    public async Task<IActionResult> AddItem(int id, CreateShoppingListItemDto createShoppingListItemDto)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var shoppingListExists = await _context.ShoppingLists
            .AnyAsync(shoppingList => shoppingList.Id == id && shoppingList.UserId == userId);

        if (!shoppingListExists)
        {
            return NotFound(new
            {
                Message = "Shopping list not found."
            });
        }

        var customName = createShoppingListItemDto.CustomName?.Trim();
        var hasIngredient = createShoppingListItemDto.IngredientId.HasValue;
        var hasCustomName = !string.IsNullOrWhiteSpace(customName);

        if (hasIngredient == hasCustomName)
        {
            return BadRequest(new
            {
                Message = "Select an ingredient or enter a custom item name."
            });
        }

        if (createShoppingListItemDto.Quantity.HasValue
            && createShoppingListItemDto.Quantity.Value <= 0)
        {
            return BadRequest(new
            {
                Message = "Quantity must be greater than zero."
            });
        }

        if (hasIngredient && (!createShoppingListItemDto.Quantity.HasValue
            || !createShoppingListItemDto.UnitId.HasValue))
        {
            return BadRequest(new
            {
                Message = "Quantity and unit are required for ingredient items."
            });
        }

        if (hasIngredient)
        {
            var ingredientExists = await _context.Ingredients
                .AnyAsync(ingredient => ingredient.Id == createShoppingListItemDto.IngredientId);

            if (!ingredientExists)
            {
                return BadRequest(new
                {
                    Message = "Ingredient does not exist."
                });
            }
        }

        if (createShoppingListItemDto.UnitId.HasValue)
        {
            var unitExists = await _context.Units
                .AnyAsync(unit => unit.Id == createShoppingListItemDto.UnitId);

            if (!unitExists)
            {
                return BadRequest(new
                {
                    Message = "Unit does not exist."
                });
            }
        }

        ShoppingListItem? item = null;

        if (hasIngredient)
        {
            item = await _context.ShoppingListItems
                .FirstOrDefaultAsync(item => item.ShoppingListId == id
                    && item.ShoppingList != null
                    && item.ShoppingList.UserId == userId
                    && item.IngredientId == createShoppingListItemDto.IngredientId
                    && item.UnitId == createShoppingListItemDto.UnitId
                    && item.CustomName == null
                    && item.IsGenerated == false);
        }

        if (!hasIngredient)
        {
            var normalizedCustomName = customName!.ToLower();

            item = await _context.ShoppingListItems
                .FirstOrDefaultAsync(item => item.ShoppingListId == id
                    && item.ShoppingList != null
                    && item.ShoppingList.UserId == userId
                    && item.IngredientId == null
                    && item.UnitId == createShoppingListItemDto.UnitId
                    && item.CustomName != null
                    && item.CustomName.ToLower() == normalizedCustomName
                    && item.IsGenerated == false);
        }

        if (item is null)
        {
            item = new ShoppingListItem
            {
                ShoppingListId = id,
                IngredientId = createShoppingListItemDto.IngredientId,
                CustomName = hasCustomName ? customName : null,
                Quantity = createShoppingListItemDto.Quantity,
                UnitId = createShoppingListItemDto.UnitId,
                IsChecked = false,
                IsGenerated = false
            };

            _context.ShoppingListItems.Add(item);
        }
        else
        {
            if (createShoppingListItemDto.Quantity.HasValue)
            {
                item.Quantity = (item.Quantity ?? 0) + createShoppingListItemDto.Quantity.Value;
            }
        }

        await _context.SaveChangesAsync();

        return Ok(await GetShoppingListItemResponseDto(item.Id, userId));
    }

    [HttpPatch("items/{itemId}/toggle")]
    public async Task<IActionResult> ToggleItem(int itemId)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var item = await GetOwnedShoppingListItem(itemId, userId);

        if (item is null)
        {
            return NotFound(new
            {
                Message = "Shopping list item not found."
            });
        }

        item.IsChecked = !item.IsChecked;
        await _context.SaveChangesAsync();

        return Ok(await GetShoppingListItemResponseDto(item.Id, userId));
    }

    [HttpPut("items/{itemId}")]
    public async Task<IActionResult> UpdateItem(int itemId, UpdateShoppingListItemDto updateShoppingListItemDto)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        if (updateShoppingListItemDto.Quantity.HasValue
            && updateShoppingListItemDto.Quantity.Value <= 0)
        {
            return BadRequest(new
            {
                Message = "Quantity must be greater than zero."
            });
        }

        if (updateShoppingListItemDto.UnitId.HasValue)
        {
            var unitExists = await _context.Units.AnyAsync(unit => unit.Id == updateShoppingListItemDto.UnitId);

            if (!unitExists)
            {
                return BadRequest(new
                {
                    Message = "Unit does not exist."
                });
            }
        }

        var item = await GetOwnedShoppingListItem(itemId, userId);

        if (item is null)
        {
            return NotFound(new
            {
                Message = "Shopping list item not found."
            });
        }

        if (item.IngredientId.HasValue
            && (!updateShoppingListItemDto.Quantity.HasValue
                || !updateShoppingListItemDto.UnitId.HasValue))
        {
            return BadRequest(new
            {
                Message = "Quantity and unit are required for ingredient items."
            });
        }

        var duplicateItem = await _context.ShoppingListItems
            .FirstOrDefaultAsync(existingItem => existingItem.Id != item.Id
                && existingItem.ShoppingListId == item.ShoppingListId
                && existingItem.ShoppingList != null
                && existingItem.ShoppingList.UserId == userId
                && existingItem.IngredientId == item.IngredientId
                && existingItem.UnitId == updateShoppingListItemDto.UnitId
                && existingItem.CustomName == item.CustomName
                && existingItem.IsGenerated == item.IsGenerated);

        if (duplicateItem is not null)
        {
            if (updateShoppingListItemDto.Quantity.HasValue)
            {
                duplicateItem.Quantity = (duplicateItem.Quantity ?? 0)
                    + updateShoppingListItemDto.Quantity.Value;
            }

            _context.ShoppingListItems.Remove(item);

            await _context.SaveChangesAsync();

            return Ok(await GetShoppingListItemResponseDto(duplicateItem.Id, userId));
        }

        item.Quantity = updateShoppingListItemDto.Quantity;
        item.UnitId = updateShoppingListItemDto.UnitId;

        await _context.SaveChangesAsync();

        return Ok(await GetShoppingListItemResponseDto(item.Id, userId));
    }

    [HttpDelete("items/{itemId}")]
    public async Task<IActionResult> DeleteItem(int itemId)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var item = await GetOwnedShoppingListItem(itemId, userId);

        if (item is null)
        {
            return NotFound(new
            {
                Message = "Shopping list item not found."
            });
        }

        _context.ShoppingListItems.Remove(item);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            Message = "Shopping list item deleted successfully."
        });
    }

    private async Task<ShoppingListResponseDto?> GetShoppingListResponseDto(int id, string userId)
    {
        return await _context.ShoppingLists
            .Where(shoppingList => shoppingList.Id == id && shoppingList.UserId == userId)
            .Select(shoppingList => new ShoppingListResponseDto
            {
                Id = shoppingList.Id,
                Name = shoppingList.Name,
                CreatedAt = shoppingList.CreatedAt,
                Items = shoppingList.Items
                    .OrderBy(item => item.Ingredient != null
                        && item.Ingredient.IngredientCategory != null
                            ? item.Ingredient.IngredientCategory.Name
                            : null)
                    .ThenBy(item => item.Ingredient != null
                        ? item.Ingredient.Name
                        : item.CustomName)
                    .Select(item => new ShoppingListItemResponseDto
                    {
                        Id = item.Id,
                        IngredientId = item.IngredientId,
                        IngredientName = item.Ingredient != null ? item.Ingredient.Name : null,
                        CustomName = item.CustomName,
                        ItemName = item.Ingredient != null ? item.Ingredient.Name : item.CustomName,
                        IngredientCategoryId = item.Ingredient != null
                            && item.Ingredient.IngredientCategory != null
                                ? item.Ingredient.IngredientCategory.Id
                                : null,
                        IngredientCategoryName = item.Ingredient != null
                            && item.Ingredient.IngredientCategory != null
                                ? item.Ingredient.IngredientCategory.Name
                                : null,
                        Quantity = item.Quantity,
                        UnitId = item.UnitId,
                        UnitName = item.Unit != null ? item.Unit.Name : null,
                        UnitShortName = item.Unit != null ? item.Unit.ShortName : null,
                        IsChecked = item.IsChecked,
                        IsGenerated = item.IsGenerated == true
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    private static string GetDefaultShoppingListName()
    {
        return $"Shopping List {DateTime.UtcNow:yyyy-MM-dd HH:mm}";
    }

    private async Task<ShoppingList> GetOrCreateShoppingList(string userId, string? name = null)
    {
        var shoppingList = await _context.ShoppingLists
            .Where(shoppingList => shoppingList.UserId == userId)
            .OrderBy(shoppingList => shoppingList.CreatedAt)
            .FirstOrDefaultAsync();

        if (shoppingList is not null)
        {
            return shoppingList;
        }

        shoppingList = new ShoppingList
        {
            UserId = userId,
            Name = name ?? GetDefaultShoppingListName(),
            CreatedAt = DateTime.UtcNow
        };

        _context.ShoppingLists.Add(shoppingList);
        await _context.SaveChangesAsync();

        return shoppingList;
    }

    private async Task<int?> GetActiveShoppingListId(string userId)
    {
        return await _context.ShoppingLists
            .Where(shoppingList => shoppingList.UserId == userId)
            .OrderBy(shoppingList => shoppingList.CreatedAt)
            .Select(shoppingList => (int?)shoppingList.Id)
            .FirstOrDefaultAsync();
    }

    private async Task SyncGeneratedShoppingListItems(
        int shoppingListId,
        string userId,
        List<ShoppingListItem> items)
    {
        var existingItems = await _context.ShoppingListItems
            .Where(item => item.ShoppingListId == shoppingListId
                && item.ShoppingList != null
                && item.ShoppingList.UserId == userId
                && item.IsGenerated == true)
            .ToListAsync();

        var legacyItems = await _context.ShoppingListItems
            .Where(item => item.ShoppingListId == shoppingListId
                && item.ShoppingList != null
                && item.ShoppingList.UserId == userId
                && item.IsGenerated == null)
            .ToListAsync();

        foreach (var item in items)
        {
            var existingItem = existingItems.FirstOrDefault(existingItem =>
                existingItem.IngredientId == item.IngredientId
                && existingItem.UnitId == item.UnitId);

            if (existingItem is not null)
            {
                existingItem.Quantity = item.Quantity;
                continue;
            }

            var legacyItem = legacyItems.FirstOrDefault(legacyItem =>
                legacyItem.IngredientId == item.IngredientId
                && legacyItem.UnitId == item.UnitId);

            if (legacyItem is not null)
            {
                legacyItem.Quantity = item.Quantity;
                legacyItem.IsGenerated = true;
                existingItems.Add(legacyItem);
                legacyItems.Remove(legacyItem);
                continue;
            }

            item.ShoppingListId = shoppingListId;
            item.IsGenerated = true;
            _context.ShoppingListItems.Add(item);
            existingItems.Add(item);
        }

        var currentGeneratedKeys = items
            .Select(item => new
            {
                item.IngredientId,
                item.UnitId
            })
            .ToList();

        var generatedItemsToRemove = existingItems
            .Where(existingItem => !currentGeneratedKeys.Any(currentItem =>
                currentItem.IngredientId == existingItem.IngredientId
                && currentItem.UnitId == existingItem.UnitId))
            .ToList();

        _context.ShoppingListItems.RemoveRange(generatedItemsToRemove);
    }

    private static List<ShoppingListItem> BuildMergedShoppingListItems(List<MealPlan> mealPlans)
    {
        return mealPlans
            .Where(mealPlan => mealPlan.Recipe is not null)
            .SelectMany(mealPlan => mealPlan.Recipe!.RecipeIngredients.Select(recipeIngredient => new
            {
                recipeIngredient.IngredientId,
                recipeIngredient.UnitId,
                Quantity = mealPlan.Recipe.Servings > 0
                    ? recipeIngredient.Quantity * mealPlan.Servings / mealPlan.Recipe.Servings
                    : recipeIngredient.Quantity
            }))
            .GroupBy(item => new { item.IngredientId, item.UnitId })
            .Select(group => new ShoppingListItem
            {
                IngredientId = group.Key.IngredientId,
                UnitId = group.Key.UnitId,
                Quantity = group.Sum(item => item.Quantity),
                IsChecked = false,
                IsGenerated = true
            })
            .ToList();
    }

    private async Task<ShoppingListItem?> GetOwnedShoppingListItem(int itemId, string userId)
    {
        return await _context.ShoppingListItems
            .Include(item => item.ShoppingList)
            .FirstOrDefaultAsync(item => item.Id == itemId
                && item.ShoppingList != null
                && item.ShoppingList.UserId == userId);
    }

    private async Task<ShoppingListItemResponseDto?> GetShoppingListItemResponseDto(int itemId, string userId)
    {
        return await _context.ShoppingListItems
            .Where(item => item.Id == itemId
                && item.ShoppingList != null
                && item.ShoppingList.UserId == userId)
            .Select(item => new ShoppingListItemResponseDto
            {
                Id = item.Id,
                IngredientId = item.IngredientId,
                IngredientName = item.Ingredient != null ? item.Ingredient.Name : null,
                CustomName = item.CustomName,
                ItemName = item.Ingredient != null ? item.Ingredient.Name : item.CustomName,
                IngredientCategoryId = item.Ingredient != null
                    && item.Ingredient.IngredientCategory != null
                        ? item.Ingredient.IngredientCategory.Id
                        : null,
                IngredientCategoryName = item.Ingredient != null
                    && item.Ingredient.IngredientCategory != null
                        ? item.Ingredient.IngredientCategory.Name
                        : null,
                Quantity = item.Quantity,
                UnitId = item.UnitId,
                UnitName = item.Unit != null ? item.Unit.Name : null,
                UnitShortName = item.Unit != null ? item.Unit.ShortName : null,
                IsChecked = item.IsChecked,
                IsGenerated = item.IsGenerated == true
            })
            .FirstOrDefaultAsync();
    }
}
