using Brandora.Web.Models.Domain;

namespace Brandora.Web.Models.Proposals;

public class ProposalListViewModel
{
    public List<Proposal> Proposals { get; set; } = new();
    public int? CampaignId { get; set; }
    public ProposalStatus? Status { get; set; }
    public Campaign? Campaign { get; set; }

    public string? Search { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    public List<Campaign> AvailableCampaigns { get; set; } = new();

    public int AllCount { get; set; }
    public int PendingCount { get; set; }
    public int AcceptedCount { get; set; }
    public int RejectedCount { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 6;
    public int TotalCount { get; set; }
    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
