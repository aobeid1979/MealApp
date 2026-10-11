using System.Text.Json;
using System.Text.Json.Serialization;
using MealApp.Services.AgentTools;

namespace MealApp.Services.Agent;

public interface IAgentModelClient
{
    Task<ModelMessage> ChatAsync(IReadOnlyList<ModelMessage> messages, IReadOnlyList<ToolDefinition> tools, CancellationToken ct);
}

public record ModelFunction(string Name, JsonElement Arguments);
public record ModelToolCall(ModelFunction Function);
public record ModelMessage(string Role, string Content)
{
    [JsonPropertyName("tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ModelToolCall>? ToolCalls { get; init; }

    [JsonPropertyName("tool_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolName { get; init; }
}

public class AgentModelException(string code, string safeMessage) : Exception(safeMessage)
{
    public string Code { get; } = code;
}
