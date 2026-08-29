using Brandora.Web.Models.Domain;

namespace Brandora.Web.Models.Messages;

public class InboxViewModel
{
    public List<Conversation> Conversations { get; set; } = new();
    public Dictionary<int, int> UnreadCounts { get; set; } = new();

    public string? Search { get; set; }
    public int TotalCount { get; set; }
    public int TotalUnread { get; set; }

    public int UnreadFor(int conversationId) => UnreadCounts.TryGetValue(conversationId, out var count) ? count : 0;
}
