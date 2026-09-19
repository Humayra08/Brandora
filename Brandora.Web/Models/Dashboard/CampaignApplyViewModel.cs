using System.ComponentModel.DataAnnotations;
using Brandora.Web.Models.Domain;

namespace Brandora.Web.Models.Dashboard;

public class CampaignApplyViewModel : CampaignApplyInputModel
{
    public List<Notification> Notifications { get; set; } = new();
    public InfluencerProfile Profile { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string? Platform { get; set; }
    public string? Niche { get; set; }
    public decimal Budget { get; set; }
    public DateTime? Deadline { get; set; }
    public int ApplicantCount { get; set; }

    public int Step { get; set; } = 2;
}

public class CampaignApplyInputModel
{
    public int CampaignId { get; set; }

    [Required(ErrorMessage = "Share your creative idea for this campaign.")]
    [StringLength(1000)]
    public string Concept { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tell the brand why you're a good fit.")]
    [StringLength(500)]
    public string WhyGoodFit { get; set; } = string.Empty;

    public string? InstagramLink { get; set; }
    public string? TikTokLink { get; set; }
    public string? YouTubeLink { get; set; }

    public string? InstagramProfileUrl { get; set; }
    public string? FacebookProfileUrl { get; set; }
    public string? TikTokProfileUrl { get; set; }

    public bool Confirmed { get; set; }
}

public class CampaignApplicationSubmittedViewModel : CampaignApplyViewModel
{
    public int ProposalId { get; set; }
    public DateTime SubmittedAt { get; set; }
    public ProposalStatus Status { get; set; }
    public string? BrandLogoUrl { get; set; }
}
