using System.Text.Json.Serialization;

namespace MealApp.DTOs.Agent;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class CreateAgentConversationRequest { }
public record ConversationSummary(Guid Id, string Title, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
public record ConversationList(IReadOnlyList<ConversationSummary> Items, int Page, bool HasMore);
public record ConversationMessage(Guid Id, string Role, string Content, long Sequence, DateTimeOffset CreatedAtUtc);
public record ConversationDetails(ConversationSummary Conversation, IReadOnlyList<ConversationMessage> Messages,
    bool HasMore, long? NextBeforeSequence);
public record ConversationResult(int StatusCode, object? Data, IReadOnlyList<string> Errors);
