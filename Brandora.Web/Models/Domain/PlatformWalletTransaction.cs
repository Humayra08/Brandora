namespace Brandora.Web.Models.Domain;

public enum PlatformWalletTransactionType
{
    CashOut,       // Admin sends platform-earned money out of the platform wallet
    Compensation   // Admin sends platform-earned money to a Brand or Influencer to settle a dispute
}

// The only storage for real platform-wallet OUTFLOWS. There is no payout/disbursement API
// on either gateway (bKash/Nagad only support Checkout — customer pays merchant), so every
// row here is a human admin sending real money in their own bKash/Nagad app and logging the
// real transaction reference afterwards. Inflows (BrandFeeAmount, WithdrawalRequest.FeeAmount)
// already have real columns and are NOT duplicated here.
public class PlatformWalletTransaction
{
    public int Id { get; set; }

    public PlatformWalletTransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public PayoutMethodKind Method { get; set; }
    public string AccountDetail { get; set; } = string.Empty;

    // The real transaction reference the admin got from their own bKash/Nagad app.
    public string GatewayReference { get; set; } = string.Empty;

    // Compensation recipient — exactly one of these is set, or neither for a CashOut.
    public int? RecipientBrandProfileId { get; set; }
    public BrandProfile? RecipientBrandProfile { get; set; }
    public int? RecipientInfluencerProfileId { get; set; }
    public InfluencerProfile? RecipientInfluencerProfile { get; set; }
    public int? DisputeId { get; set; }
    public Dispute? Dispute { get; set; }

    public string? Note { get; set; }
    public string ProcessedByAdmin { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
