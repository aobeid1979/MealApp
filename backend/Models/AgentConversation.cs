using System.ComponentModel.DataAnnotations;

namespace MealApp.Models;

public class AgentConversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required] public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    [Required, MaxLength(100)] public string Title { get; set; } = "New conversation";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public long NextSequence { get; set; } = 1;
    public Guid? ActiveTurnId { get; set; }
    [ConcurrencyCheck] public Guid Revision { get; set; } = Guid.NewGuid();
    public ICollection<AgentMessage> Messages { get; set; } = new List<AgentMessage>();
    public ICollection<AgentConversationTurn> Turns { get; set; } = new List<AgentConversationTurn>();
}
