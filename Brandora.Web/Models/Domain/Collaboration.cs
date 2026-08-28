namespace Brandora.Web.Models.Domain;

public class Collaboration
{
    public int Id { get; set; }

    public int ProposalId { get; set; }
    public Proposal Proposal { get; set; } = null!;

    public int CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;

    public int InfluencerProfileId { get; set; }
    public InfluencerProfile InfluencerProfile { get; set; } = null!;

    public CollaborationStatus Status { get; set; } = CollaborationStatus.Active;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Milestone> Milestones { get; set; } = new List<Milestone>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
