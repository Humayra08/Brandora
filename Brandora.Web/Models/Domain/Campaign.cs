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
    public DateTime? StartDate { get; set; }
    public DateTime? Deadline { get; set; }
    public string? ContentGuidelines { get; set; }

    // Banner (cover image) — used as the campaign's cover in cards, lists and tables.
    public string? MediaUrl { get; set; }
    public string? MediaType { get; set; }

    // Optional campaign video / reel — shown on the campaign's details page (Brand and
    // creator side), separate from the banner.
    public string? VideoUrl { get; set; }

    // Optional link to where this campaign is already live on social media
    // (a Facebook / Instagram / TikTok / YouTube post, etc.). Shown to creators on
    // the campaign's details page as a clickable "View on <platform>" link.
    public string? SocialPostUrl { get; set; }

    // Targeting & audience (campaign wizard Step 3). Each field maps to a
    // real InfluencerProfile attribute so a "fit" evaluation is always a
    // transparent, database-derived comparison — never a fabricated score.
    public string? TargetLocation { get; set; }
    public int? TargetFollowersMin { get; set; }
    public int? TargetFollowersMax { get; set; }
    public decimal? TargetEngagementRateMin { get; set; }
    public bool TargetVerifiedOnly { get; set; }

    // Set once the wizard's Targeting step (Step 3) has been saved at least
    // once, so Review & Publish (Step 4) can tell "no targeting rules" apart
    // from "targeting step not reached yet".
    public bool TargetingConfigured { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Proposal> Proposals { get; set; } = new List<Proposal>();
    public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
    public ICollection<CampaignMilestonePlan> MilestonePlans { get; set; } = new List<CampaignMilestonePlan>();
}
