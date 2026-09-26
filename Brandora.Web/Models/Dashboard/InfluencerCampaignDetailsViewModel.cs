using Brandora.Web.Models.Domain;

namespace Brandora.Web.Models.Dashboard;

public class InfluencerCampaignDetailsViewModel
{
    public List<Notification> Notifications { get; set; } = new();
    public InfluencerProfile Profile { get; set; } = null!;

    public int CampaignId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string? BrandLogoUrl { get; set; }
    public string BrandIndustry { get; set; } = string.Empty;
    public string? BrandWebsiteUrl { get; set; }

    // The brand's own social profiles (Brand Settings → Social Profiles), shown as icons.
    public List<(SocialPlatform Platform, string Url)> BrandSocialLinks { get; set; } = new();

    // Optional link to where the brand has already posted this campaign on social media.
    public string? SocialPostUrl { get; set; }

    // The campaign's banner image and its separate video / reel (played on this page).
    public string? BannerUrl { get; set; }
    public string? VideoUrl { get; set; }
    public string? Platform { get; set; }
    public string? Niche { get; set; }
    public decimal Budget { get; set; }
    public DateTime? Deadline { get; set; }
    public DateTime CreatedAt { get; set; }
    public CampaignStatus Status { get; set; }
    public int ApplicantCount { get; set; }

    public bool BrandVerified { get; set; }
    public int? MyProposalId { get; set; }
    public List<CampaignDetailsMilestone> Milestones { get; set; } = new();

    public bool CanApply { get; set; }
    public ProposalStatus? MyProposalStatus { get; set; }
    public bool IsCollaborating { get; set; }
    public bool IsCollabCompleted { get; set; }
}

public class CampaignDetailsMilestone
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public DateTime? DueDate { get; set; }
    public MilestoneStatus Status { get; set; }
}
