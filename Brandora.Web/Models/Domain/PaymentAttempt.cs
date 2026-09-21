namespace Brandora.Web.Models.Domain;

// One row per gateway checkout session a Brand starts for a Payment. A Payment can have
// several attempts (e.g. the Brand cancels on bKash's page and tries again) — Attempts are
// the audit trail of what actually happened at the gateway; Payment.Status only reflects
// the outcome of whichever attempt succeeded.
public class PaymentAttempt
{
    public int Id { get; set; }

    public int PaymentId { get; set; }
    public Payment Payment { get; set; } = null!;

    public string Gateway { get; set; } = string.Empty; // "bKash", "Nagad", ...
    public string? GatewayPaymentId { get; set; }
    public string? GatewayTransactionId { get; set; }

    public PaymentAttemptStatus Status { get; set; } = PaymentAttemptStatus.Created;
    public string? FailureReason { get; set; }

    // Raw gateway response bodies, kept verbatim for audit/debugging — never parsed back
    // out for business logic, only for a human reviewing what the gateway actually said.
    public string? CreateResponseJson { get; set; }
    public string? ExecuteResponseJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

public enum PaymentAttemptStatus
{
    Created,
    AwaitingCustomer,
    Completed,
    Failed,
    Cancelled
}
