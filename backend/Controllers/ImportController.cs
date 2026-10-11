using MealApp.DTOs.Import;
using MealApp.Services.Import;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MealApp.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/import")]
[RequestSizeLimit(2 * 1024 * 1024)]
public class ImportController(DataImportService service) : ApiControllerBase
{
    [HttpPost("ingredients/preview")]
    public Task<IActionResult> PreviewIngredients(IngredientImportRequest request, CancellationToken cancellationToken)
        => Ingredients(request, true, cancellationToken);

    [HttpPost("ingredients")]
    public Task<IActionResult> ImportIngredients(IngredientImportRequest request, CancellationToken cancellationToken)
        => Ingredients(request, false, cancellationToken);

    [HttpPost("recipes/preview")]
    public Task<IActionResult> PreviewRecipes(RecipeImportRequest request, CancellationToken cancellationToken)
        => Recipes(request, true, cancellationToken);

    [HttpPost("recipes")]
    public Task<IActionResult> ImportRecipes(RecipeImportRequest request, CancellationToken cancellationToken)
        => Recipes(request, false, cancellationToken);

    private async Task<IActionResult> Ingredients(IngredientImportRequest request, bool preview, CancellationToken ct)
    {
        if (request.Items is not { Count: > 0 and <= 100 })
            return BadRequest("Provide between 1 and 100 items per import.");
        return Ok(await service.IngredientsAsync(request.Items, preview, ct));
    }

    private async Task<IActionResult> Recipes(RecipeImportRequest request, bool preview, CancellationToken ct)
    {
        if (request.Items is not { Count: > 0 and <= 100 })
            return BadRequest("Provide between 1 and 100 items per import.");
        return Ok(await service.RecipesAsync(request.Items, preview, ct));
    }
}
