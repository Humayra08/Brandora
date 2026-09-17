using Brandora.Web.Models.Domain;

namespace Brandora.Web.Models.Influencers;

public class CreatorListViewModel
{
    public List<InfluencerProfile> Creators { get; set; } = new();
    public HashSet<int> ShortlistedIds { get; set; } = new();

    public string? Search { get; set; }
    public string? Niche { get; set; }
    public string? Platform { get; set; }
    public string? Sort { get; set; }

    public int TotalCount { get; set; }

    // Real, database-derived hero figures — never fabricated constants.
    public decimal AvgEngagementRate { get; set; }
    public int PlatformCount { get; set; }

    // Populated when the brand is viewing Discovery in the context of a
    // specific campaign (?campaignId=…), so each card can show a
    // transparent, rule-based "Campaign Fit" score instead of a bare list.
    public List<Campaign> AvailableCampaigns { get; set; } = new();
    public Campaign? SelectedCampaign { get; set; }
    public Dictionary<int, (int Score, List<string> Reasons)> FitByCreatorId { get; set; } = new();

    public bool IsShortlisted(int creatorId) => ShortlistedIds.Contains(creatorId);
}
