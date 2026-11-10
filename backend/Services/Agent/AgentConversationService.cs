using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using MealApp.Data;
using MealApp.DTOs.Agent;
using MealApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MealApp.Services.Agent;

public class AgentConversationService(IServiceScopeFactory scopes, IHttpContextAccessor accessor,
    AgentChatService agent, IOptions<AgentOptions> options, ILogger<AgentConversationService> logger)
{
    private string? Owner => accessor.HttpContext?.User.Identity?.IsAuthenticated == true
        ? accessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) : null;

    public async Task<ConversationResult> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        if (Owner is not { } owner) return Error(401, "Authentication is required.");
        if (page is < 1 or > 10000 || pageSize is < 1 or > 50) return Error(400, "Use page 1-10000 and pageSize 1-50.");
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rows = await db.AgentConversations.AsNoTracking().Where(x => x.UserId == owner)
            .OrderByDescending(x => x.UpdatedAtUtc).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize + 1)
            .Select(x => new ConversationSummary(x.Id, x.Title, x.CreatedAtUtc, x.UpdatedAtUtc)).ToListAsync(ct);
        return new(200, new ConversationList(rows.Take(pageSize).ToArray(), page, rows.Count > pageSize), []);
    }

    public async Task<ConversationResult> CreateAsync(CancellationToken ct)
    {
        if (Owner is not { } owner) return Error(401, "Authentication is required.");
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var conversation = new AgentConversation { UserId = owner };
        db.AgentConversations.Add(conversation);
        await db.SaveChangesAsync(ct);
        return new(200, Summary(conversation), []);
    }

    public async Task<ConversationResult> GetAsync(Guid id, long? beforeSequence, int pageSize, CancellationToken ct)
    {
        if (Owner is not { } owner) return Error(401, "Authentication is required.");
        if (pageSize is < 1 or > 100 || beforeSequence is <= 0) return Error(400, "Use pageSize 1-100 and a positive beforeSequence.");
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var conversation = await db.AgentConversations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.UserId == owner, ct);
        if (conversation is null) return Missing();
        var rows = await db.AgentMessages.AsNoTracking().Where(x => x.ConversationId == id && (!beforeSequence.HasValue || x.Sequence < beforeSequence))
            .OrderByDescending(x => x.Sequence).Take(pageSize + 1)
            .Select(x => new ConversationMessage(x.Id, x.Role, x.Content, x.Sequence, x.CreatedAtUtc)).ToListAsync(ct);
        var messages = rows.Take(pageSize).Reverse().ToArray();
        return new(200, new ConversationDetails(Summary(conversation), messages, rows.Count > pageSize,
            rows.Count > pageSize ? messages[0].Sequence : null), []);
    }

    public async Task<ConversationResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        if (Owner is not { } owner) return Error(401, "Authentication is required.");
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var conversation = await db.AgentConversations.AsSplitQuery().Include(x => x.Messages).Include(x => x.Turns)
            .SingleOrDefaultAsync(x => x.Id == id && x.UserId == owner, ct);
        if (conversation is null) return Missing();
        db.AgentConversations.Remove(conversation);
        await db.SaveChangesAsync(ct);
        return new(200, new { Message = "Conversation deleted." }, []);
    }

    public async Task<ConversationResult> ChatAsync(AgentChatRequest request, CancellationToken ct)
    {
        if (Owner is not { } owner) return Error(401, "Authentication is required.");
        if (request.ConversationId is not { } id || id == Guid.Empty || request.RequestId is not { } requestId || requestId == Guid.Empty)
            return Error(400, "A valid conversationId and requestId are required together.");
        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > 2000)
            return Error(400, "Provide a message between 1 and 2000 characters.");
        var settings = options.Value;
        if (!settings.IsValid()) return Error(503, "The local assistant is not configured or enabled.");
        var message = request.Message.Trim();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(message)));
        Guid turnId;
        List<ModelMessage> history;
        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var conversation = await db.AgentConversations.SingleOrDefaultAsync(x => x.Id == id && x.UserId == owner, ct);
            if (conversation is null) return Missing();
            var previous = await db.AgentConversationTurns.SingleOrDefaultAsync(x => x.ConversationId == id && x.RequestId == requestId, ct);
            if (previous is not null)
            {
                if (previous.MessageHash != hash) return Error(409, "This requestId belongs to a different message.");
                if (previous.Status == "Completed")
                {
                    var answer = await db.AgentMessages.Where(x => x.ConversationId == id && x.TurnId == previous.Id && x.Role == "Assistant")
                        .Select(x => x.Content).SingleAsync(ct);
                    return new(200, new AgentChatResponse(answer, []) { ConversationId = id, RequestId = requestId }, []);
                }
                return Error(409, previous.Status == "Processing"
                    ? "This turn is processing or awaiting recovery. Reload before retrying; it will not execute twice."
                    : "This attempt failed. Start a new attempt to retry.", previous.Status == "Failed");
            }
            if (conversation.ActiveTurnId is not null) return Error(409, "Another turn is processing in this conversation. Reload before retrying.");
            var stored = await db.AgentMessages.AsNoTracking().Where(x => x.ConversationId == id)
                .OrderByDescending(x => x.Sequence).Take(settings.MaxHistoryMessages).ToListAsync(ct);
            history = SelectHistory(stored, settings.MaxHistoryCharacters);
            var turn = new AgentConversationTurn { ConversationId = id, RequestId = requestId, MessageHash = hash };
            turnId = turn.Id;
            db.AgentConversationTurns.Add(turn);
            conversation.ActiveTurnId = turn.Id;
            conversation.Revision = Guid.NewGuid();
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException) { return Error(409, "The conversation changed. Reload before retrying the same request."); }
        }

        AgentRunResult outcome;
        try { outcome = await agent.RunWithHistoryAsync(message, history, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await MarkFailedAsync(id, owner, turnId);
            throw;
        }
        if (outcome.StatusCode != 200)
        {
            await MarkFailedAsync(id, owner, turnId);
            return new(outcome.StatusCode, new { RequestState = "Failed" }, outcome.Errors);
        }

        // One short atomic SaveChanges after Ollama, on a context separate from tool execution.
        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var conversation = await db.AgentConversations.SingleOrDefaultAsync(x => x.Id == id && x.UserId == owner, ct);
            if (conversation is null) return Missing();
            if (conversation.ActiveTurnId != turnId) return Error(409, "This conversation changed while the assistant was responding.");
            var turn = await db.AgentConversationTurns.SingleAsync(x => x.Id == turnId && x.ConversationId == id, ct);
            var now = DateTimeOffset.UtcNow;
            db.AgentMessages.AddRange(new AgentMessage { ConversationId = id, TurnId = turnId, Sequence = conversation.NextSequence,
                Role = "User", Content = message, CreatedAtUtc = now },
                new AgentMessage { ConversationId = id, TurnId = turnId, Sequence = conversation.NextSequence + 1,
                    Role = "Assistant", Content = outcome.Response.Answer, CreatedAtUtc = now });
            if (conversation.NextSequence == 1)
            {
                var title = string.Join(" ", message.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
                conversation.Title = title[..Math.Min(100, title.Length)];
            }
            conversation.NextSequence += 2;
            conversation.UpdatedAtUtc = now;
            conversation.ActiveTurnId = null;
            conversation.Revision = Guid.NewGuid();
            turn.Status = "Completed";
            try { await db.SaveChangesAsync(ct); }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                logger.LogError("Conversation turn persistence failed; retry must use the same request ID.");
                return Error(503, "Unable to save this turn. Retry with the same requestId or reload; do not submit it as a new attempt.");
            }
        }
        return new(200, outcome.Response with { ConversationId = id, RequestId = requestId }, []);
    }

    private async Task MarkFailedAsync(Guid id, string owner, Guid turnId)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var conversation = await db.AgentConversations.SingleOrDefaultAsync(x => x.Id == id && x.UserId == owner, deadline.Token);
            if (conversation?.ActiveTurnId != turnId) return;
            var turn = await db.AgentConversationTurns.SingleAsync(x => x.Id == turnId && x.ConversationId == id, deadline.Token);
            turn.Status = "Failed";
            conversation.ActiveTurnId = null;
            conversation.Revision = Guid.NewGuid();
            await db.SaveChangesAsync(deadline.Token);
        }
        catch (Exception) { logger.LogError("Unable to finalize a failed conversation turn; it remains reserved for safety."); }
    }

    private static List<ModelMessage> SelectHistory(List<AgentMessage> stored, int budget)
    {
        var selected = new List<AgentMessage>();
        foreach (var pair in stored.GroupBy(x => x.TurnId).OrderByDescending(x => x.Max(m => m.Sequence)))
        {
            var messages = pair.OrderBy(x => x.Sequence).ToArray();
            if (messages.Length != 2 || messages[0].Role != "User" || messages[1].Role != "Assistant") continue;
            var size = messages.Sum(x => x.Content.Length);
            if (size > budget) break;
            budget -= size;
            selected.AddRange(messages);
        }
        return selected.OrderBy(x => x.Sequence).Select(x => new ModelMessage(x.Role == "User" ? "user" : "assistant", x.Content)).ToList();
    }

    private static ConversationSummary Summary(AgentConversation c) => new(c.Id, c.Title, c.CreatedAtUtc, c.UpdatedAtUtc);
    private static ConversationResult Missing() => Error(404, "Conversation not found.");
    private static ConversationResult Error(int status, string message, bool failed = false)
        => new(status, failed ? new { RequestState = "Failed" } : null, [message]);
}
