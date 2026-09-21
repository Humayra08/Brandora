namespace Brandora.Web.Models.Domain;

public class Milestone
{
    public int Id { get; set; }

    public int CollaborationId { get; set; }
    public Collaboration Collaboration { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public DateTime? DueDate { get; set; }
    public MilestoneStatus Status { get; set; } = MilestoneStatus.Pending;

    public string? ProofUrl { get; set; }
    public string? ProofNotes { get; set; }

    // Two-stage approval: Brand and Admin each sign off independently before a milestone
    // is payment-eligible (see PaymentsController.Release, which requires BOTH
    // Status == Approved (Admin's side, via AdminProofReviewController) AND
    // BrandApprovedAt != null (Brand's side, via MilestonesController) — whichever
    // reviewer acts first, the other still has to sign off before money can move.
    public DateTime? BrandApprovedAt { get; set; }
    public string? BrandRevisionReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Payment? Payment { get; set; }
}
