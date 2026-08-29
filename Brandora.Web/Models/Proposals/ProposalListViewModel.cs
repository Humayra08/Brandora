using Brandora.Web.Models.Domain;

namespace Brandora.Web.Models.Proposals;

public class ProposalListViewModel
{
    public List<Proposal> Proposals { get; set; } = new();
    public int? CampaignId { get; set; }
    public ProposalStatus? Status { get; set; }
    public Campaign? Campaign { get; set; }
}
