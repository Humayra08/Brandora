using Brandora.Web.Models.Domain;

namespace Brandora.Web.Models.Brands;

public class BrandListViewModel
{
    public List<BrandProfile> Brands { get; set; } = new();

    public string? Search { get; set; }
    public string? Industry { get; set; }
    public int TotalCount { get; set; }
}
