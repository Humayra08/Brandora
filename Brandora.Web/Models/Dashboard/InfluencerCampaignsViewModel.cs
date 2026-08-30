using Brandora.Web.Models.Domain;

namespace Brandora.Web.Models.Dashboard;

public class BrowseCampaignRow
{
    public int CampaignId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string? Platform { get; set; }
    public string? Niche { get; set; }
    public decimal Budget { get; set; }
    public DateTime? Deadline { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? MediaUrl { get; set; }
    public CampaignStatus Status { get; set; }
    public int ApplicantCount { get; set; }
    public ProposalStatus? MyProposalStatus { get; set; }
    public bool IsCollaborating { get; set; }
    public bool IsCollabCompleted { get; set; }
}

public class InfluencerCampaignsViewModel
{
    public InfluencerProfile Profile { get; set; } = null!;

    public List<BrowseCampaignRow> Campaigns { get; set; } = new();
    public List<string> PlatformOptions { get; set; } = new();
    public List<string> CategoryOptions { get; set; } = new();

    public string? Search { get; set; }
    public string? Category { get; set; }
    public string? Platform { get; set; }
    public string? Budget { get; set; }
    public string? Sort { get; set; }
    public string Tab { get; set; } = "all";

    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 6;
    public int TotalFilteredCount { get; set; }
    public int TotalPages => TotalFilteredCount == 0 ? 1 : (int)Math.Ceiling(TotalFilteredCount / (double)PageSize);

    public int AllCount { get; set; }
    public int AppliedCount { get; set; }
    public int InReviewCount { get; set; }
    public int OngoingCount { get; set; }
    public int CompletedCount { get; set; }
}
