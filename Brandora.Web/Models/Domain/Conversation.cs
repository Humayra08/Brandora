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

    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
