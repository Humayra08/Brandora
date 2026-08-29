using Brandora.Web.Models.Domain;
using Brandora.Web.Models.Influencers;

namespace Brandora.Web.Models.Dashboard;

public class CampaignSummaryItem
{
    public Campaign Campaign { get; set; } = null!;
    public int ApplicantCount { get; set; }
    public int CollaborationCount { get; set; }
    public List<string> CreatorNames { get; set; } = new();
    public int ProgressPercent { get; set; }
}

public class AttentionItem
{
    public string Kind { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string LinkUrl { get; set; } = string.Empty;
    public string ActionLabel { get; set; } = string.Empty;
}

public class CollaborationSummaryItem
{
    public Collaboration Collaboration { get; set; } = null!;
    public int TotalMilestones { get; set; }
    public int PaidMilestones { get; set; }
    public DateTime? NextDueDate { get; set; }
    public bool NeedsAttention { get; set; }
}

public class UpcomingItem
{
    public string Kind { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string LinkUrl { get; set; } = string.Empty;
}

public class MonthlySpendPoint
{
    public string Month { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

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
    public int ShortlistedCount { get; set; }
    public int ActiveCollaborationCount { get; set; }

    public decimal PendingPaymentsAmount { get; set; }
    public decimal CompletedPaymentsAmount { get; set; }

    public List<Campaign> RecentCampaigns { get; set; } = new();
    public List<CampaignSummaryItem> CampaignWorkspace { get; set; } = new();

    public List<AttentionItem> AttentionItems { get; set; } = new();

    public List<InfluencerProfile> CreatorPreview { get; set; } = new();
    public HashSet<int> ShortlistedIds { get; set; } = new();

    public Campaign? SmartMatchCampaign { get; set; }
    public List<SmartMatchResult> SmartMatchPreview { get; set; } = new();

    public List<CollaborationSummaryItem> CollaborationActivity { get; set; } = new();

    public List<UpcomingItem> UpcomingItems { get; set; } = new();

    public List<Notification> RecentActivity { get; set; } = new();

    public List<MonthlySpendPoint> SpendByMonth { get; set; } = new();

    public bool HasAnyCampaigns => DraftCount + PublishedCount + ActiveCount + CompletedCount > 0;
    public bool IsShortlisted(int creatorId) => ShortlistedIds.Contains(creatorId);
}
