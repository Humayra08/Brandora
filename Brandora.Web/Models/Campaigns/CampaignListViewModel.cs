using Brandora.Web.Models.Domain;

namespace Brandora.Web.Models.Campaigns;

public class CampaignListViewModel
{
    public List<Campaign> Campaigns { get; set; } = new();
    public Dictionary<int, int> ApplicantCounts { get; set; } = new();

    public string? Search { get; set; }
    public CampaignStatus? Status { get; set; }
    public string? Platform { get; set; }
    public string? Sort { get; set; }

    public int DraftCount { get; set; }
    public int PublishedCount { get; set; }
    public int ActiveCount { get; set; }
    public int CompletedCount { get; set; }
    public int TotalCount { get; set; }

    public int ApplicantsFor(int campaignId) => ApplicantCounts.TryGetValue(campaignId, out var count) ? count : 0;
}
