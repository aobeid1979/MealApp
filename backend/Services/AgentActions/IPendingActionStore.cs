using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MealApp.DTOs.AgentActions;

namespace MealApp.Services.AgentActions;

public enum PendingActionStatus { Pending, Approved, Rejected, Executed, Failed }

public interface IPendingActionStore
{
    PendingAction Add(string requestedByUserId, PreparedRecipeDraft draft);
    PendingAction? Find(Guid id);
    IReadOnlyList<PendingActionResponse> ListPending();
}

public sealed class PendingAction
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private volatile PendingActionStatus _status = PendingActionStatus.Pending;
    public Guid Id { get; } = Guid.NewGuid();
    public string ActionType { get; } = "create_recipe_draft";
    public string RequestedByUserId { get; }
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
    public string PayloadJson { get; }
    public string ReferenceBinding { get; }
    public string PayloadHash { get; }
    public PendingActionStatus Status { get => _status; internal set => _status = value; }
    public string? DecidedByUserId { get; internal set; }
    public int? RecipeId { get; internal set; }
    internal SemaphoreSlim Mutex { get; } = new(1, 1);

    internal PendingAction(string requestedByUserId, PreparedRecipeDraft draft)
    {
        RequestedByUserId = requestedByUserId;
        PayloadJson = JsonSerializer.Serialize(draft.Payload, JsonOptions);
        ReferenceBinding = draft.ReferenceBinding;
        PayloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        { Id, ActionType, RequestedByUserId, PayloadJson, ReferenceBinding }))));
    }

    public PendingActionResponse Snapshot() => new(Id, ActionType, RequestedByUserId, CreatedAt, Status.ToString(),
        ToolPolicyDecision.RequireApproval.ToString(), JsonSerializer.Deserialize<JsonElement>(PayloadJson), PayloadHash, DecidedByUserId, RecipeId);
}
