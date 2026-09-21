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

    // The gateway's own identifier for this payment session (e.g. bKash's paymentID),
    // set when a PaymentAttempt is created for this Payment. Distinct from
    // TransactionReference, which is the gateway's final trxID once money has moved.
    public string? GatewayPaymentId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PaymentAttempt> Attempts { get; set; } = new List<PaymentAttempt>();
}
