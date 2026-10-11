using MealApp.DTOs.AgentActions;

namespace MealApp.Services.AgentActions;

public class InMemoryPendingActionStore : IPendingActionStore
{
    private readonly Dictionary<Guid, PendingAction> _actions = [];
    private readonly object _sync = new();

    public PendingAction Add(string requestedByUserId, PreparedRecipeDraft draft)
    {
        lock (_sync)
        {
            if (_actions.Count >= 1000) throw new InvalidOperationException("Pending action store capacity reached.");
            var action = new PendingAction(requestedByUserId, draft);
            _actions.Add(action.Id, action);
            return action;
        }
    }

    public PendingAction? Find(Guid id)
    {
        lock (_sync) return _actions.GetValueOrDefault(id);
    }

    public IReadOnlyList<PendingActionResponse> ListPending()
    {
        lock (_sync) return _actions.Values.Where(x => x.Status == PendingActionStatus.Pending)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => x.Snapshot()).ToArray();
    }
}
