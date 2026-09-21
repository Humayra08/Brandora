using Brandora.Web.Models.Domain;

namespace Brandora.Web.Models.Dashboard;

public class PaymentHistoryRow
{
    public DateTime Date { get; set; }
    public int CampaignId { get; set; }
    public string CampaignTitle { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string MilestoneTitle { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; }

    // The Payment entity tracks no gateway details, so these stay null outside preview mode.
    public string? Method { get; set; }
    public string? TransactionId { get; set; }
}

public class PayoutMethodRow
{
    public string Name { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}

public class InfluencerPaymentsViewModel
{
    public List<Notification> Notifications { get; set; } = new();
    public InfluencerProfile Profile { get; set; } = null!;

    public decimal TotalEarnings { get; set; }
    public decimal PendingAmount { get; set; }
    public decimal CompletedAmount { get; set; }

    // Credited earnings not yet withdrawn, and the platform fee taken at withdrawal.
    public decimal AvailableBalance { get; set; }
    public decimal WithdrawalFeePercent { get; set; }
    public int PendingCount { get; set; }
    public int CompletedCount { get; set; }
    public int? GrowthPercent { get; set; }

    public List<PaymentHistoryRow> Payments { get; set; } = new();
    public List<PayoutMethodRow> PayoutMethods { get; set; } = new();

    public string Status { get; set; } = "all";
    public string Range { get; set; } = "all";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 5;
    public int TotalCount { get; set; }
    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public int FirstRowNumber => TotalCount == 0 ? 0 : (Page - 1) * PageSize + 1;
    public int LastRowNumber => Math.Min(Page * PageSize, TotalCount);

    // Mirrors WithdrawalViewModel: renders the reference design with example values
    // behind ?preview=true, never as the creator's real payment record.
    public bool IsPreview { get; set; }
}
