using Brandora.Web.Models.Domain;

namespace Brandora.Web.Models.Dashboard;

public class ActiveCampaignRow
{
    public int CollaborationId { get; set; }
    public string CampaignTitle { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string? Platform { get; set; }
    public decimal TotalValue { get; set; }
    public int ProgressPercent { get; set; }
    public int CompletedMilestones { get; set; }
    public int TotalMilestones { get; set; }
}

public class UpcomingMilestoneRow
{
    public int MilestoneId { get; set; }
    public string CampaignTitle { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string MilestoneTitle { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime? DueDate { get; set; }
    public MilestoneStatus Status { get; set; }
}

public class EarningsPoint
{
    public string Label { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class InfluencerDashboardViewModel
{
    public InfluencerProfile Profile { get; set; } = null!;

    public int ActiveCampaignCount { get; set; }
    public int CompletedCampaignCount { get; set; }
    public int ProfileStrengthPercent { get; set; }

    public decimal PendingEarnings { get; set; }
    public decimal TotalEarnings { get; set; }

    public decimal MonthPaid { get; set; }
    public decimal MonthPending { get; set; }
    public decimal MonthTotal => MonthPaid + MonthPending;

    public List<ActiveCampaignRow> ActiveCampaigns { get; set; } = new();
    public List<UpcomingMilestoneRow> UpcomingMilestones { get; set; } = new();
    public List<EarningsPoint> EarningsSeries { get; set; } = new();
}
