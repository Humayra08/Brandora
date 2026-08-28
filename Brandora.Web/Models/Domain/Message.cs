namespace Brandora.Web.Models.Domain;

public class Message
{
    public int Id { get; set; }

    public int ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;

    public string SenderUserId { get; set; } = string.Empty;
    public ApplicationUser SenderUser { get; set; } = null!;

    public string Body { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}
