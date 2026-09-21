using System.ComponentModel.DataAnnotations.Schema;

namespace Brandora.Web.Models.Domain;

public class Payment
{
    public int Id { get; set; }

    public int CollaborationId { get; set; }
    public Collaboration Collaboration { get; set; } = null!;

    public int? MilestoneId { get; set; }
    public Milestone? Milestone { get; set; }

    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public DateTime? PaidAt { get; set; }

    public PaymentMethod? Method { get; set; }
    public string? TransactionReference { get; set; }
    public EscrowStatus EscrowStatus { get; set; } = EscrowStatus.Held;

    // Amount is always the gross figure the Brand pays (matches Milestone.Amount).
    // PlatformFeeAmount/NetAmount are set once, at settlement time, by
    // PaymentSettlementService — never computed ad hoc in a view or controller, so the
    // commission rate used is always whatever was actually configured at the moment the
    // money moved, not whatever the config says right now.
    public decimal PlatformFeeAmount { get; set; }
    public decimal NetAmount { get; set; }

    // Two-sided commission: the Brand pays the milestone amount PLUS this fee on top
    // (fixed when the payment is released, at the rate configured then). It goes to the
    // platform wallet; the creator is credited the full milestone Amount and pays their
    // own share only when withdrawing (see WalletService.QuoteWithdrawal).
    // Payments settled before this model existed have BrandFeeAmount = 0 and carry their
    // creator-side deduction in PlatformFeeAmount/NetAmount instead.
    public decimal BrandFeeAmount { get; set; }

    // The creator's commission rate for THIS payment, locked when it's released (the same
    // moment as BrandFeeAmount). Charged when the creator withdraws these earnings, so a
    // later change to the platform rate never affects money already earned.
    // Null only for payments created before rates were locked.
    public decimal? CreatorFeePercent { get; set; }

    // What the Brand is actually charged at checkout.
    [NotMapped]
    public decimal TotalCharged => Amount + BrandFeeAmount;

    // The gateway's own identifier for this payment session (e.g. bKash's paymentID),
    // set when a PaymentAttempt is created for this Payment. Distinct from
    // TransactionReference, which is the gateway's final trxID once money has moved.
    public string? GatewayPaymentId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PaymentAttempt> Attempts { get; set; } = new List<PaymentAttempt>();
}
