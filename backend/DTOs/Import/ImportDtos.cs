namespace MealApp.DTOs.Import;

public class IngredientImportRequest
{
    public List<IngredientImportItem?>? Items { get; set; }
}

public class IngredientImportItem
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string? IngredientCategory { get; set; }
}

public class RecipeImportRequest
{
    public List<RecipeImportItem?>? Items { get; set; }
}

public class RecipeImportItem
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string? Category { get; set; }
    public int Servings { get; set; }
    public int PreparationTimeMinutes { get; set; }
    public int CookingTimeMinutes { get; set; }
    public List<RecipeImportIngredient?>? Ingredients { get; set; }
    public List<RecipeImportStep?>? Steps { get; set; }
    public List<string?>? Tools { get; set; }
}

public class RecipeImportIngredient
{
    public string? Ingredient { get; set; }
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
}

public class RecipeImportStep
{
    public int StepNumber { get; set; }
    public string? Instruction { get; set; }
}

public class ImportResult
{
    public int TotalReceived { get; set; }
    public int Imported { get; set; }
    public int Ready { get; set; }
    public int Skipped { get; set; }
    public int Duplicates { get; set; }
    public bool IsPreview { get; set; }
    public List<ImportValidationError> ValidationErrors { get; set; } = [];
    public List<ImportRowResult> Rows { get; set; } = [];
}

public record ImportValidationError(int Row, string Field, string Message);
public record ImportRowResult(int Row, string Name, string Status, int? Id = null);
