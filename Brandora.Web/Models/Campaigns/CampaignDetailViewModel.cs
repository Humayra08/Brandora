using Brandora.Web.Models.Domain;

namespace Brandora.Web.Models.Campaigns;

public class CampaignDetailViewModel
{
    public Campaign Campaign { get; set; } = null!;
    public int ApplicantCount { get; set; }
    public int CollaborationCount { get; set; }
    public int ConversationCount { get; set; }
}
