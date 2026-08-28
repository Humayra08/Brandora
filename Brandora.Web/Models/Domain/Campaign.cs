namespace Brandora.Web.Models.Domain;

public class Campaign
{
    public int Id { get; set; }

    public int BrandProfileId { get; set; }
    public BrandProfile BrandProfile { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public CampaignStatus Status { get; set; } = CampaignStatus.Draft;

    public decimal Budget { get; set; }
    public decimal SpentAmount { get; set; }

    public string? Platform { get; set; }
    public string? Niche { get; set; }
    public DateTime? Deadline { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Proposal> Proposals { get; set; } = new List<Proposal>();
    public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
}
