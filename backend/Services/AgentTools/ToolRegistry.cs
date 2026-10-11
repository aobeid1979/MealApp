using System.Collections.ObjectModel;
using System.Text.Json;

namespace MealApp.Services.AgentTools;

public enum ToolCapability { Read, Write }
public record ToolDefinition(string Name, string Description, JsonElement InputSchema, ToolCapability Capability)
{
    public bool ReadOnly => Capability == ToolCapability.Read;
}

public class ToolRegistry
{
    public const int DefaultLimit = 10;
    public const int MaximumLimit = 50;
    private readonly IReadOnlyDictionary<string, ToolDefinition> _definitions;
    public IEnumerable<ToolDefinition> Definitions => _definitions.Values;

    public ToolRegistry()
    {
        var searchSchema = JsonSerializer.SerializeToElement(new
        {
            type = "object", additionalProperties = false,
            properties = new
            {
                query = new { type = "string", maxLength = 200 },
                category = new { type = "string", maxLength = 200 },
                limit = new { type = "integer", minimum = 1, maximum = MaximumLimit, @default = DefaultLimit }
            }
        });
        var idSchema = JsonSerializer.SerializeToElement(new
        {
            type = "object", additionalProperties = false, required = new[] { "recipeId" },
            properties = new { recipeId = new { type = "integer", minimum = 1, maximum = int.MaxValue } }
        });
        ToolDefinition[] tools =
        [
            new("search_recipes", "Search recipe titles/descriptions, optionally by category name; ordered by title then ID.", searchSchema, ToolCapability.Read),
            new("get_recipe", "Read a complete recipe by ID, including ordered ingredients, steps and tools.", idSchema, ToolCapability.Read),
            new("list_ingredients", "Search ingredient names, optionally by ingredient category name; ordered by name then ID.", searchSchema, ToolCapability.Read),
            new("get_recipe_ingredients", "Read the stored ingredient quantities and units of a recipe, without scaling.", idSchema, ToolCapability.Read),
            new("create_recipe_draft", "Propose a recipe using existing category, ingredient and unit names. Admin only. Creates a pending proposal, not a published recipe. Separate human Admin approval is required; never claim publication.", DraftSchema(), ToolCapability.Write)
        ];
        _definitions = new ReadOnlyDictionary<string, ToolDefinition>(tools.ToDictionary(x => x.Name, StringComparer.Ordinal));
    }

    public bool TryGet(string name, out ToolDefinition definition) => _definitions.TryGetValue(name, out definition!);

    public IReadOnlyList<string> Validate(ToolDefinition definition, JsonElement arguments)
    {
        var errors = new List<string>();
        if (arguments.ValueKind != JsonValueKind.Object) return ["Arguments must be a JSON object."];
        ValidateValue(definition.InputSchema, arguments, "arguments", errors);
        return errors;
    }

    private static void ValidateValue(JsonElement schema, JsonElement arguments, string path, List<string> errors)
    {
        var type = schema.GetProperty("type");
        if (arguments.ValueKind == JsonValueKind.Null && type.ValueKind == JsonValueKind.Array
            && type.EnumerateArray().Any(x => x.GetString() == "null")) return;
        var kind = type.ValueKind == JsonValueKind.Array ? type[0].GetString() : type.GetString();
        if (kind == "string")
        {
            if (arguments.ValueKind != JsonValueKind.String || arguments.GetString()!.Length > schema.GetProperty("maxLength").GetInt32())
                errors.Add($"{path} must be a string of at most {schema.GetProperty("maxLength")} characters.");
            return;
        }
        if (kind is "integer" or "number")
        {
            var valid = arguments.ValueKind == JsonValueKind.Number && arguments.TryGetDecimal(out var number)
                && number >= schema.GetProperty("minimum").GetDecimal() && number <= schema.GetProperty("maximum").GetDecimal();
            if (kind == "integer") valid = valid && arguments.TryGetInt32(out _);
            if (!valid) errors.Add($"{path} must be a {kind} between {schema.GetProperty("minimum")} and {schema.GetProperty("maximum")}.");
            return;
        }
        if (kind == "array")
        {
            if (arguments.ValueKind != JsonValueKind.Array || arguments.GetArrayLength() < schema.GetProperty("minItems").GetInt32()
                || arguments.GetArrayLength() > schema.GetProperty("maxItems").GetInt32())
            { errors.Add($"{path} has an invalid collection size."); return; }
            var index = 0;
            foreach (var value in arguments.EnumerateArray()) ValidateValue(schema.GetProperty("items"), value, $"{path}[{index++}]", errors);
            return;
        }
        if (arguments.ValueKind != JsonValueKind.Object) { errors.Add($"{path} must be a JSON object."); return; }
        var properties = schema.GetProperty("properties");
        var supplied = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in arguments.EnumerateObject())
        {
            if (!supplied.Add(property.Name)) { errors.Add("Duplicate argument properties are not allowed."); continue; }
            if (!properties.TryGetProperty(property.Name, out var rule))
            {
                errors.Add("Unknown arguments are not allowed, including identity or authorization fields.");
                continue;
            }
            ValidateValue(rule, property.Value, path + "." + property.Name, errors);
        }
        if (schema.TryGetProperty("required", out var required))
            foreach (var property in required.EnumerateArray())
                if (!supplied.Contains(property.GetString()!)) errors.Add($"{property.GetString()} is required.");
    }

    private static JsonElement DraftSchema() => JsonDocument.Parse("""
    {
      "type":"object","additionalProperties":false,
      "required":["name","category","servings","preparationTimeMinutes","cookingTimeMinutes","ingredients","steps"],
      "properties":{
        "name":{"type":"string","maxLength":200},
        "description":{"type":["string","null"],"maxLength":5000},
        "imageUrl":{"type":["string","null"],"maxLength":2048},
        "category":{"type":"string","maxLength":200},
        "servings":{"type":"integer","minimum":1,"maximum":50},
        "preparationTimeMinutes":{"type":"integer","minimum":0,"maximum":10080},
        "cookingTimeMinutes":{"type":"integer","minimum":0,"maximum":10080},
        "ingredients":{"type":"array","minItems":1,"maxItems":100,"items":{
          "type":"object","additionalProperties":false,"required":["ingredient","quantity","unit"],
          "properties":{"ingredient":{"type":"string","maxLength":200},"quantity":{"type":"number","minimum":0.01,"maximum":9999999999999999.99},"unit":{"type":"string","maxLength":200}}
        }},
        "steps":{"type":"array","minItems":1,"maxItems":100,"items":{
          "type":"object","additionalProperties":false,"required":["stepNumber","instruction"],
          "properties":{"stepNumber":{"type":"integer","minimum":1,"maximum":2147483647},"instruction":{"type":"string","maxLength":5000}}
        }},
        "tools":{"type":"array","minItems":0,"maxItems":100,"items":{"type":"string","maxLength":200}}
      }
    }
    """).RootElement.Clone();
}
