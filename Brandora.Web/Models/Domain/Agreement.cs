namespace Brandora.Web.Models.Domain;

// The Campaign Collaboration Agreement generated once a brand accepts a proposal.
// ContentHtml is frozen at generation time (Version 1.0) and never edited — a new
// Agreement (new Code/Version) would be created instead if terms ever needed to change.
public class Agreement
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;
    public decimal Version { get; set; } = 1.0m;

    public int CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;

    public int ProposalId { get; set; }
    public Proposal Proposal { get; set; } = null!;

    public int BrandProfileId { get; set; }
    public BrandProfile BrandProfile { get; set; } = null!;

    public int InfluencerProfileId { get; set; }
    public InfluencerProfile InfluencerProfile { get; set; } = null!;

    public AgreementStatus Status { get; set; } = AgreementStatus.AwaitingBrandSignature;

    public string ContentHtml { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AgreementSignature> Signatures { get; set; } = new List<AgreementSignature>();
    public ICollection<AgreementEvent> Events { get; set; } = new List<AgreementEvent>();
}

public class AgreementSignature
{
    public int Id { get; set; }

    public int AgreementId { get; set; }
    public Agreement Agreement { get; set; } = null!;

    public AgreementParty Party { get; set; }
    public string SignerUserId { get; set; } = string.Empty;
    public string SignerName { get; set; } = string.Empty;
    public string SignatureImageUrl { get; set; } = string.Empty;
    public SignatureMethod Method { get; set; }
    public DateTime SignedAt { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
}

// Audit trail — backend evidence of who did what and when, kept separate from the
// document itself so the PDF/document view stays clean.
public class AgreementEvent
{
    public int Id { get; set; }

    public int AgreementId { get; set; }
    public Agreement Agreement { get; set; } = null!;

    public string EventType { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string? Detail { get; set; }
}
