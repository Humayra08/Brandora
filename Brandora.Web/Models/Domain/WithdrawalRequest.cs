namespace Brandora.Web.Models.Domain;

public class WithdrawalRequest
{
    public int Id { get; set; }

    public int InfluencerProfileId { get; set; }
    public InfluencerProfile InfluencerProfile { get; set; } = null!;

    // Amount = what leaves the creator's balance. FeeAmount = the platform's share,
    // computed by WalletService.QuoteWithdrawal so that across ANY number of partial
    // withdrawals the total fee never exceeds FeePercent of what was earned.
    // PayoutAmount = what is actually sent to the creator (Amount - FeeAmount).
    public decimal Amount { get; set; }
    public decimal FeePercent { get; set; }
    public decimal FeeAmount { get; set; }
    public decimal PayoutAmount { get; set; }
    public PayoutMethodKind Method { get; set; }
    public string AccountDetail { get; set; } = string.Empty;
    public WithdrawalStatus Status { get; set; } = WithdrawalStatus.Pending;

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }

    // Set when an admin manually sends the payout in their own bKash/Nagad app and logs
    // the real transaction ID here (there is no payout/disbursement API to call instead —
    // see PlatformWalletTransaction for why). ProcessedByAdmin records who did it.
    public string? TransactionReference { get; set; }
    public string? ProcessedByAdmin { get; set; }
}
