namespace Brandora.Web.Models.Domain;

// The influencer's earnings ledger. This is the source of truth for "how much has this
// influencer actually earned" — never Payment rows summed ad hoc, and never "completed
// minus withdrawals" computed inline in a controller. One row per completed payment
// (Type = Earning, Amount = NetAmount after platform commission). Nothing here mutates
// once written; corrections are new rows (Type = Adjustment), never edits.
public class WalletTransaction
{
    public int Id { get; set; }

    public int InfluencerProfileId { get; set; }
    public InfluencerProfile InfluencerProfile { get; set; } = null!;

    public WalletTransactionType Type { get; set; }

    // Positive for money in (Earning, a credit Adjustment), negative for money out
    // (a debit Adjustment). Withdrawals themselves are tracked by the influencer's own
    // WithdrawalRequest flow, not duplicated here.
    public decimal Amount { get; set; }

    public int? PaymentId { get; set; }
    public Payment? Payment { get; set; }

    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum WalletTransactionType
{
    Earning,
    Adjustment
}
