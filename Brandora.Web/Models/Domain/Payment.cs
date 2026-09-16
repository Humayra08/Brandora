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

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
