namespace Brandora.Web.Models.Settings;

public class NotificationPreferencesFormViewModel
{
    public bool NewProposals { get; set; }
    public bool ProposalUpdates { get; set; }
    public bool Messages { get; set; }
    public bool MilestoneUpdates { get; set; }
    public bool PaymentUpdates { get; set; }
    public bool CampaignDeadlines { get; set; }
}
