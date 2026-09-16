using Brandora.Web.Models.Domain;

namespace Brandora.Web.Models.Campaigns;

public class CampaignReviewViewModel
{
    public Campaign Campaign { get; set; } = null!;
    public List<CampaignMilestonePlan> MilestonePlans { get; set; } = [];

    // A real, database-derived count of creators currently in Brandora who
    // match the campaign's targeting rules right now — never a fabricated
    // "estimated reach" figure. Null when no targeting rules were set.
    public int? MatchingCreatorCount { get; set; }

    public decimal TotalPlanned => MilestonePlans.Sum(p => p.Amount);
    public bool OverBudget => TotalPlanned > Campaign.Budget;

    public bool HasTargeting =>
        !string.IsNullOrWhiteSpace(Campaign.TargetLocation)
        || Campaign.TargetFollowersMin.HasValue
        || Campaign.TargetFollowersMax.HasValue
        || Campaign.TargetEngagementRateMin.HasValue
        || Campaign.TargetVerifiedOnly;
}
