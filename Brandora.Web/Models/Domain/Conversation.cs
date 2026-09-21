namespace Brandora.Web.Models.Domain;

public class Conversation
{
    public int Id { get; set; }

    public int? CampaignId { get; set; }
    public Campaign? Campaign { get; set; }

    public int BrandProfileId { get; set; }
    public BrandProfile BrandProfile { get; set; } = null!;

    public int InfluencerProfileId { get; set; }
    public InfluencerProfile InfluencerProfile { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Each side manages its own copy of the thread. Pinned keeps it at the top of that
    // side's inbox. Cleared ("Delete conversation") hides everything up to that moment
    // for that side only — the other side keeps its full history, and if a new message
    // arrives later the thread reappears for the side that cleared it, showing only
    // what came after.
    public DateTime? BrandPinnedAt { get; set; }
    public DateTime? BrandClearedAt { get; set; }
    public DateTime? InfluencerPinnedAt { get; set; }
    public DateTime? InfluencerClearedAt { get; set; }

    public ICollection<Message> Messages { get; set; } = new List<Message>();

    public DateTime? ClearedAtFor(bool brandSide) => brandSide ? BrandClearedAt : InfluencerClearedAt;
    public DateTime? PinnedAtFor(bool brandSide) => brandSide ? BrandPinnedAt : InfluencerPinnedAt;

    // Messages this side can still see (everything after its own "delete", if any).
    public IEnumerable<Message> VisibleMessages(bool brandSide)
    {
        var cleared = ClearedAtFor(brandSide);
        return cleared is null ? Messages : Messages.Where(m => m.SentAt > cleared.Value);
    }
}
