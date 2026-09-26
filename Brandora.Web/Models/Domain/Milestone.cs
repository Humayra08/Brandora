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

    // The link to the creator's live social media post for this milestone (Upload Proof →
    // "Post Link"). Kept separately from ProofUrl so a post link survives even when the
    // creator also uploads a screenshot or video as the proof file.
    public string? ProofPostUrl { get; set; }

    /// <summary>
    /// The public post this milestone went live as: the saved post link, or — for proof
    /// submitted before post links were stored separately — the proof itself when it is a
    /// link to a social platform. Null when there is no live post to open.
    /// </summary>
    public string? LivePostUrl()
    {
        if (IsWebLink(ProofPostUrl))
        {
            return ProofPostUrl;
        }

        return IsWebLink(ProofUrl) && SocialPlatform.FromUrl(ProofUrl) is not null ? ProofUrl : null;
    }

    private static bool IsWebLink(string? url) =>
        !string.IsNullOrWhiteSpace(url) &&
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

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

/// <summary>
/// Model for the shared "live on &lt;platform&gt;" callout (Views/Shared/_LivePostCard).
/// <paramref name="Heading"/> may contain "{platform}", replaced with the platform's name.
/// </summary>
public sealed record LivePostCard(string Url, string Heading);
