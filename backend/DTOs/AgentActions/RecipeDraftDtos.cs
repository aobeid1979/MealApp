using System.Text.Json;
using System.Text.Json.Serialization;
using MealApp.DTOs.Import;

namespace MealApp.DTOs.AgentActions;

public record PreparedRecipeDraft(RecipeImportItem Payload, string ReferenceBinding);
public record RecipeDraftValidation(ImportResult Report, PreparedRecipeDraft? Draft);
public record PendingActionResponse(Guid ActionId, string ActionType, string RequestedByUserId,
    DateTimeOffset CreatedAt, string Status, string PolicyDecision, JsonElement Payload, string PayloadHash,
    string? DecidedByUserId, int? RecipeId);
public record ActionOperationResult(int StatusCode, object? Data, IReadOnlyList<string> Errors);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class ActionDecisionRequest { }
