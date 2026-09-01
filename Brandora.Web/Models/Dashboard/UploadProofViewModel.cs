using Brandora.Web.Models.Domain;

namespace Brandora.Web.Models.Dashboard;

public class ProofMilestoneOption
{
    public int MilestoneId { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class ProofCampaignOption
{
    public int CollaborationId { get; set; }
    public int CampaignId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string? Platform { get; set; }
    public string? Niche { get; set; }
    public string? MediaUrl { get; set; }
    public decimal Budget { get; set; }
    public DateTime? Deadline { get; set; }
    public List<ProofMilestoneOption> Milestones { get; set; } = new();
}

public class UploadProofViewModel
{
    public InfluencerProfile Profile { get; set; } = null!;
    public List<ProofCampaignOption> Campaigns { get; set; } = new();
}
