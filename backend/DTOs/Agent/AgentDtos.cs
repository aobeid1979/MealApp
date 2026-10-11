using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MealApp.DTOs.Agent;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class AgentChatRequest
{
    [Required]
    [StringLength(2000)]
    public string Message { get; set; } = "";
}

public record AgentToolCallSummary(string Tool, bool IsSuccess, string Code);
public record AgentChatResponse(string Answer, IReadOnlyList<AgentToolCallSummary> ToolCalls)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? PendingAction { get; init; }
}
public record AgentRunResult(int StatusCode, string Code, AgentChatResponse Response, IReadOnlyList<string> Errors);
