namespace Brandora.Web.Models.Domain;

public class Dispute
{
    public int Id { get; set; }

    public int CollaborationId { get; set; }
    public Collaboration Collaboration { get; set; } = null!;

    public int? MilestoneId { get; set; }
    public Milestone? Milestone { get; set; }

    public int BrandProfileId { get; set; }
    public BrandProfile BrandProfile { get; set; } = null!;

    public int InfluencerProfileId { get; set; }
    public InfluencerProfile InfluencerProfile { get; set; } = null!;

    public string Reason { get; set; } = string.Empty;
    public DisputeStatus Status { get; set; } = DisputeStatus.Open;
    public string? ResolutionNotes { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
