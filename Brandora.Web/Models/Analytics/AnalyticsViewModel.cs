namespace Brandora.Web.Models.Analytics;

public class CampaignStatusSlice
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
    public string Color { get; set; } = string.Empty;
}

public class MonthlySpend
{
    public string Month { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class TopCreator
{
    public string Name { get; set; } = string.Empty;
    public int InfluencerProfileId { get; set; }
    public decimal TotalPaid { get; set; }
}

public class AnalyticsViewModel
{
    public decimal TotalSpend { get; set; }
    public decimal TotalBudget { get; set; }
    public int TotalCampaigns { get; set; }
    public int ActiveCollaborations { get; set; }

    public int ProposalsSent { get; set; }
    public int ProposalsAccepted { get; set; }
    public int ProposalsRejected { get; set; }
    public int ProposalsPending { get; set; }

    public List<CampaignStatusSlice> StatusBreakdown { get; set; } = new();
    public List<MonthlySpend> SpendByMonth { get; set; } = new();
    public List<TopCreator> TopCreators { get; set; } = new();

    public int DecidedProposals => ProposalsAccepted + ProposalsRejected;
    public int AcceptanceRatePercent => DecidedProposals == 0 ? 0 : (int)Math.Round(ProposalsAccepted * 100m / DecidedProposals);
}
