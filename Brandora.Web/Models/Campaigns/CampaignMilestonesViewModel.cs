using Brandora.Web.Models.Domain;

namespace Brandora.Web.Models.Campaigns;

public class CampaignMilestonesViewModel
{
    public Campaign Campaign { get; set; } = null!;
    public List<CampaignMilestonePlan> Plans { get; set; } = [];

    public decimal TotalPlanned => Plans.Sum(p => p.Amount);
    public decimal Remaining => Campaign.Budget - TotalPlanned;
    public bool OverBudget => TotalPlanned > Campaign.Budget;
}
