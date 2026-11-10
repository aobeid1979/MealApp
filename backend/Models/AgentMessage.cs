using System.ComponentModel.DataAnnotations;

namespace MealApp.Models;

public class AgentMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public AgentConversation Conversation { get; set; } = null!;
    public Guid TurnId { get; set; }
    public long Sequence { get; set; }
    [Required, MaxLength(9)] public string Role { get; set; } = "User";
    [Required, MaxLength(16000)] public string Content { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
