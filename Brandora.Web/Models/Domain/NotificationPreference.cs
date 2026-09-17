namespace Brandora.Web.Models.Domain;

// One row per user (Brand or Influencer), created lazily with all-enabled
// defaults the first time Settings is opened. NotificationService checks
// these flags before creating a Notification row, so turning a category off
// genuinely stops that notification from being generated — not just hidden
// in the UI.
public class NotificationPreference
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public bool NewProposals { get; set; } = true;
    public bool ProposalUpdates { get; set; } = true;
    public bool Messages { get; set; } = true;
    public bool MilestoneUpdates { get; set; } = true;
    public bool PaymentUpdates { get; set; } = true;
    public bool CampaignDeadlines { get; set; } = true;
}
