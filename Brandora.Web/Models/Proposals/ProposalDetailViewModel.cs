using Brandora.Web.Models.Domain;

namespace Brandora.Web.Models.Proposals;

public class ProposalDetailViewModel
{
    public Proposal Proposal { get; set; } = null!;
    public int CampaignMilestonePlanCount { get; set; }
    public List<Collaboration> PastCollaborations { get; set; } = new();
}
