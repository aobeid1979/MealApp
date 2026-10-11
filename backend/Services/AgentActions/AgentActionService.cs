using System.Security.Claims;
using System.Text.Json;
using MealApp.DTOs.AgentActions;
using MealApp.DTOs.Import;
using MealApp.Services.Import;
using MealApp.Models;
using Microsoft.AspNetCore.Identity;

namespace MealApp.Services.AgentActions;

public class AgentActionService(IPendingActionStore store, DataImportService importer,
    UserManager<ApplicationUser> users, IHttpContextAccessor accessor, ILogger<AgentActionService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ActionOperationResult> ProposeAsync(RecipeImportItem item, CancellationToken ct)
    {
        var userId = accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!await IsCurrentAdminAsync(userId)) return Error(403, "Only a current Admin can propose recipe drafts.");
        var validated = await importer.ValidateDraftAsync(item, ct);
        if (validated.Draft is null)
            return Error(400, validated.Report.ValidationErrors.Count > 0
                ? validated.Report.ValidationErrors.Select(x => $"{x.Field}: {x.Message}").ToArray()
                : ["Recipe title already exists."]);
        var action = store.Add(userId!, validated.Draft);
        Audit("pending_action_created", userId!, action, "approval_required");
        // The model only needs the approval reference, not internal identity or payload binding.
        return new(200, new { ActionId = action.Id, Status = "Pending", Message = "Recipe proposal created. Human Admin approval is required before publication." }, []);
    }

    public async Task<ActionOperationResult> ListPendingAsync()
    {
        var userId = accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!await IsCurrentAdminAsync(userId)) return Error(403, "Admin authorization is required.");
        return new(200, store.ListPending(), []);
    }

    public async Task<ActionOperationResult> DecideAsync(Guid id, bool approve, CancellationToken ct)
    {
        var userId = accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!await IsCurrentAdminAsync(userId)) return Error(403, "Admin authorization is required.");
        var action = store.Find(id);
        if (action is null) return Error(404, "Pending action not found.");
        Audit(approve ? "approval_requested" : "rejection_requested", userId!, action, "requested");
        await action.Mutex.WaitAsync(ct);
        try
        {
            if (action.Status != PendingActionStatus.Pending)
            {
                Audit("repeated_execution_attempt_rejected", userId!, action, "conflict");
                return Error(409, "This action has already been decided and cannot execute again.");
            }
            action.DecidedByUserId = userId;
            if (!approve)
            {
                action.Status = PendingActionStatus.Rejected;
                Audit("rejected", userId!, action, "rejected");
                return new(200, action.Snapshot(), []);
            }
            // A stale JWT or revoked requester role cannot preserve approval authority.
            if (!await IsCurrentAdminAsync(action.RequestedByUserId))
            {
                action.Status = PendingActionStatus.Failed;
                Audit("execution_failed", userId!, action, "requester_not_admin");
                return Error(403, "The requesting account is no longer an Admin. Create a new proposal.");
            }
            action.Status = PendingActionStatus.Approved;
            Audit("approved", userId!, action, "approved");
            Audit("execution_started", userId!, action, "started");
            try
            {
                var payload = JsonSerializer.Deserialize<RecipeImportItem>(action.PayloadJson, JsonOptions)!;
                var report = await importer.ExecuteApprovedDraftAsync(new(payload, action.ReferenceBinding), ct);
                if (report.Imported != 1)
                {
                    action.Status = PendingActionStatus.Failed;
                    Audit("execution_failed", userId!, action, "revalidation_failed");
                    return Error(409, report.ValidationErrors.Count > 0
                        ? report.ValidationErrors.Select(x => $"{x.Field}: {x.Message}").ToArray()
                        : ["Recipe title already exists. Create a new proposal."]);
                }
                action.RecipeId = report.Rows.Single(x => x.Status == "imported").Id;
                action.Status = PendingActionStatus.Executed;
                Audit("execution_succeeded", userId!, action, "executed");
                return new(200, action.Snapshot(), []);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                action.Status = PendingActionStatus.Failed;
                Audit("execution_failed", userId!, action, "cancelled");
                throw;
            }
            catch (Exception exception)
            {
                // Terminal failure prevents replay after an uncertain database commit.
                action.Status = PendingActionStatus.Failed;
                logger.LogError(exception, "Approved action {ActionId} execution failed.", action.Id);
                Audit("execution_failed", userId!, action, "failed");
                return Error(500, "Unable to execute the approved recipe proposal. Review the database before creating a new proposal.");
            }
        }
        finally { action.Mutex.Release(); }
    }

    private async Task<bool> IsCurrentAdminAsync(string? id)
    {
        var principal = accessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true || !principal.IsInRole("Admin") || string.IsNullOrWhiteSpace(id)) return false;
        var account = await users.FindByIdAsync(id);
        return account is not null && await users.IsInRoleAsync(account, "Admin");
    }

    private static ActionOperationResult Error(int status, params string[] errors) => new(status, null, errors);
    private void Audit(string stage, string userId, PendingAction action, string result)
        => logger.LogInformation("Agent action audit: Stage={Stage} UserId={UserId} ActionId={ActionId} ActionType={ActionType} Timestamp={Timestamp} Policy={Policy} Result={Result}",
            stage, userId, action.Id, action.ActionType, DateTimeOffset.UtcNow, ToolPolicyDecision.RequireApproval, result);
}
