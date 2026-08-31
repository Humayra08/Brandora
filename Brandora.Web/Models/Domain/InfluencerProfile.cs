namespace Brandora.Web.Models.Domain;

public class InfluencerProfile
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public string FullName { get; set; } = string.Empty;
    public string PrimaryPlatform { get; set; } = string.Empty;
    public string PlatformUsername { get; set; } = string.Empty;
    public string ContentNiche { get; set; } = string.Empty;
    public string AudienceSize { get; set; } = string.Empty;
    public bool Verified { get; set; }

    public string? Bio { get; set; }
    public string? Location { get; set; }
    public string? WebsiteUrl { get; set; }
    public int Followers { get; set; }
    public decimal EngagementRate { get; set; }
    public string? RateNote { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Proposal> Proposals { get; set; } = new List<Proposal>();
    public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
    public ICollection<ShortlistEntry> ShortlistedBy { get; set; } = new List<ShortlistEntry>();
}
