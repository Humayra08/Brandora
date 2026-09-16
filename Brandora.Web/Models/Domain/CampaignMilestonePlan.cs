namespace Brandora.Web.Models.Domain;

// A brand-authored deliverable/budget plan captured during campaign creation
// (Step 2 of the campaign wizard), before any creator has been confirmed.
// When a proposal is accepted, ProposalsController.ConfirmAccept copies the
// campaign's plan rows into real, per-collaboration Milestone rows — the
// plan itself is left in place so it can be reused for every creator working
// on the same campaign.
public class CampaignMilestonePlan
{
    public int Id { get; set; }

    public int CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public DateTime? DueDate { get; set; }
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
