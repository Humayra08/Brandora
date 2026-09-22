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

    // Who actually reported this — set once, by whichever side used their own "Report a
    // Dispute" page first. If the other side later reports the same milestone, their
    // statement is attached to this SAME dispute (see BrandDisputesController /
    // InfluencerDisputesController) rather than creating a second, duplicate case.
    public ProposalInitiator RaisedBy { get; set; }

    public string? BrandStatement { get; set; }
    public DateTime? BrandStatementAt { get; set; }
    public string? InfluencerStatement { get; set; }
    public DateTime? InfluencerStatementAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<DisputeEvidence> Evidence { get; set; } = new List<DisputeEvidence>();
    public ICollection<DisputeNote> Notes { get; set; } = new List<DisputeNote>();
}

// A file either side attached to their statement (screenshot, brief, contract, etc.) — real
// Cloudinary uploads via MediaUploadService, same as proof-of-post and campaign media.
public class DisputeEvidence
{
    public int Id { get; set; }

    public int DisputeId { get; set; }
    public Dispute Dispute { get; set; } = null!;

    public ProposalInitiator UploadedBy { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

// An admin-only internal note on a dispute — never visible to the Brand or Influencer.
public class DisputeNote
{
    public int Id { get; set; }

    public int DisputeId { get; set; }
    public Dispute Dispute { get; set; } = null!;

    public string AdminName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
