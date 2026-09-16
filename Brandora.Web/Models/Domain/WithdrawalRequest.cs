namespace Brandora.Web.Models.Domain;

public class WithdrawalRequest
{
    public int Id { get; set; }

    public int InfluencerProfileId { get; set; }
    public InfluencerProfile InfluencerProfile { get; set; } = null!;

    public decimal Amount { get; set; }
    public PayoutMethodKind Method { get; set; }
    public string AccountDetail { get; set; } = string.Empty;
    public WithdrawalStatus Status { get; set; } = WithdrawalStatus.Pending;

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
}
