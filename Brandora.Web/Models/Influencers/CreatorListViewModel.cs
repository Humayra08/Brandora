using Brandora.Web.Models.Domain;

namespace Brandora.Web.Models.Influencers;

public class CreatorListViewModel
{
    public List<InfluencerProfile> Creators { get; set; } = new();
    public HashSet<int> ShortlistedIds { get; set; } = new();

    public string? Search { get; set; }
    public string? Niche { get; set; }
    public string? Platform { get; set; }
    public string? Sort { get; set; }

    public int TotalCount { get; set; }

    public bool IsShortlisted(int creatorId) => ShortlistedIds.Contains(creatorId);
}
