using System.ComponentModel.DataAnnotations;

namespace MealApp.Models;

// Durable request receipt: no prompts, token, payload or pending-action object is stored here.
public class AgentConversationTurn
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public AgentConversation Conversation { get; set; } = null!;
    public Guid RequestId { get; set; }
    [Required, MaxLength(64)] public string MessageHash { get; set; } = "";
    [Required, MaxLength(10)] public string Status { get; set; } = "Processing";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
