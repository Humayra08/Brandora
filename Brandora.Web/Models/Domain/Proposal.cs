namespace Brandora.Web.Models.Domain;

public class Proposal
{
    public int Id { get; set; }

    public int CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;

    public int InfluencerProfileId { get; set; }
    public InfluencerProfile InfluencerProfile { get; set; } = null!;

    public ProposalInitiator InitiatedBy { get; set; }
    public decimal ProposedAmount { get; set; }
    public string Deliverables { get; set; } = string.Empty;
    public string? Timeline { get; set; }
    public string? Message { get; set; }
    public ProposalStatus Status { get; set; } = ProposalStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Collaboration? Collaboration { get; set; }
}
