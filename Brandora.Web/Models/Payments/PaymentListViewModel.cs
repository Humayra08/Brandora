using Brandora.Web.Models.Domain;

namespace Brandora.Web.Models.Payments;

public class PaymentListViewModel
{
    public List<Payment> Payments { get; set; } = new();

    // Milestones belonging to this brand's campaigns that have not been paid out yet —
    // used by the "Ongoing Milestones" preview so the Finance page shows where each
    // payment currently sits in the two-stage-approval -> release -> bKash pipeline,
    // not just a ledger of payments that have already been released.
    public List<Milestone> OngoingMilestones { get; set; } = new();
    public int OngoingTotalCount { get; set; }

    // Approved by both Brand and Admin, no Payment released yet — money the brand can
    // move right now.
    public int ReadyToReleaseCount { get; set; }
    public decimal ReadyToReleaseAmount { get; set; }

    // Proof submitted (or Admin-approved) and still waiting on the brand's own sign-off.
    public int AwaitingReviewCount { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalPending { get; set; }
    public int CompletedCount { get; set; }
    public int PendingCount { get; set; }

    public decimal TotalCampaignBudget { get; set; }
    public decimal TotalCampaignSpend { get; set; }
    public Dictionary<PaymentMethod, int> MethodCounts { get; set; } = new();

    // Read from PLATFORM_COMMISSION_PERCENT at request time (same config key
    // PaymentSettlementService uses) so this always reflects the rate actually in effect,
    // never a hardcoded figure that could drift from the real setting.
    public decimal PlatformFeePercent { get; set; }
}
