namespace Brandora.Web.Models.Domain;

public class PayoutMethod
{
    public int Id { get; set; }

    public int InfluencerProfileId { get; set; }
    public InfluencerProfile InfluencerProfile { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public PayoutMethodKind Kind { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
