using Brandora.Web.Models.Domain;

namespace Brandora.Web.Models.Campaigns;

public class CampaignDetailViewModel
{
    public Campaign Campaign { get; set; } = null!;
    public int ApplicantCount { get; set; }
    public int CollaborationCount { get; set; }
    public int ConversationCount { get; set; }

    public List<CampaignMilestonePlan> MilestonePlans { get; set; } = [];
    public int TotalMilestoneCount { get; set; }
    public int PaidMilestoneCount { get; set; }
    public decimal PendingPaymentsAmount { get; set; }

    // Milestones whose creator shared a link to the live post (newest first), shown
    // as "live on <platform>" cards. Collaboration.InfluencerProfile is loaded.
    public List<Milestone> LivePostMilestones { get; set; } = [];
}
