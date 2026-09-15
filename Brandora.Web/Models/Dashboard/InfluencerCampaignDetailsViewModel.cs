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
    public string? Platform { get; set; }
    public string? Niche { get; set; }
    public decimal Budget { get; set; }
    public DateTime? Deadline { get; set; }
    public DateTime CreatedAt { get; set; }
    public CampaignStatus Status { get; set; }
    public int ApplicantCount { get; set; }

    public bool CanApply { get; set; }
    public ProposalStatus? MyProposalStatus { get; set; }
    public bool IsCollaborating { get; set; }
    public bool IsCollabCompleted { get; set; }
}
