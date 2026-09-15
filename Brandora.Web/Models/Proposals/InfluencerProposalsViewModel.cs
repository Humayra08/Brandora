using Brandora.Web.Models.Domain;
namespace Brandora.Web.Models.Proposals;
public class InfluencerProposalsViewModel
{
    public List<Proposal> Proposals { get; set; } = [];
    public List<Notification> Notifications { get; set; } = [];
    public string InfluencerName { get; set; } = "";
    public string? Search { get; set; }
    public ProposalStatus? Status { get; set; }
    public string Sort { get; set; } = "newest";
    public int Page { get; set; } = 1;
    public int PageSize { get; } = 5;
    public int TotalCount { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}