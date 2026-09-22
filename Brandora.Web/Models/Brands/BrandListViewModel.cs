using Brandora.Web.Models.Domain;

namespace Brandora.Web.Models.Brands;

public class BrandListViewModel
{
    public List<BrandProfile> Brands { get; set; } = new();
    public List<Notification> Notifications { get; set; } = new();
    public string InfluencerName { get; set; } = "";

    public string? Search { get; set; }
    public string? Industry { get; set; }
    public int TotalCount { get; set; }
    public int VerifiedCount { get; set; }
    public int IndustryCount { get; set; }

    public Dictionary<int, int> ActiveCampaignCounts { get; set; } = new();
    public int ActiveCampaignsFor(int brandId) => ActiveCampaignCounts.TryGetValue(brandId, out var count) ? count : 0;
}
