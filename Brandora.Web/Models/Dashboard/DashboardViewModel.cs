using Brandora.Web.Models.Domain;

namespace Brandora.Web.Models.Dashboard;

public class DashboardViewModel
{
    public string CompanyName { get; set; } = string.Empty;

    public int DraftCount { get; set; }
    public int PublishedCount { get; set; }
    public int ActiveCount { get; set; }
    public int CompletedCount { get; set; }

    public decimal TotalBudget { get; set; }
    public decimal TotalSpend { get; set; }

    public int PendingProposalCount { get; set; }

    public List<Campaign> RecentCampaigns { get; set; } = new();

    public bool HasAnyCampaigns => DraftCount + PublishedCount + ActiveCount + CompletedCount > 0;
}
