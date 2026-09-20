using Brandora.Web.Models.Domain;

namespace Brandora.Web.Models.Search;

public class SearchResultsViewModel
{
    public string Query { get; set; } = string.Empty;

    public List<Campaign> Campaigns { get; set; } = new();
    public List<InfluencerProfile> Influencers { get; set; } = new();
    public List<Proposal> Applications { get; set; } = new();
    public List<Collaboration> Collaborations { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();

    public int TotalCount { get; set; }
}
