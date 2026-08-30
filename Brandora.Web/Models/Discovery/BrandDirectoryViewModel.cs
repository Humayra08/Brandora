namespace Brandora.Web.Models.Discovery;

/// <summary>
/// One brand tile in the "All Brands" grid.
/// </summary>
public class BrandCardViewModel
{
    public string CompanyName { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public int ActiveCampaigns { get; set; }

    /// <summary>
    /// Wordmark drawn in the card header. Used until <see cref="LogoImageUrl"/>
    /// is supplied, so a real asset can be dropped in without touching the view.
    /// </summary>
    public string LogoText { get; set; } = string.Empty;

    public string? LogoImageUrl { get; set; }

    /// <summary>CSS background applied to the card's logo banner.</summary>
    public string LogoBackground { get; set; } = "#1c2340";

    /// <summary>Optional override for the wordmark colour on light banners.</summary>
    public string LogoColor { get; set; } = "#ffffff";

    public string ProfileUrl { get; set; } = "#";

    public bool HasLogoImage => !string.IsNullOrWhiteSpace(LogoImageUrl);
}

/// <summary>
/// Backing model for the public "For Brands" directory page.
/// Populated from a static source today; swapping in a repository later
/// should not require changes to the Razor view.
/// </summary>
public class BrandDirectoryViewModel
{
    public List<BrandCardViewModel> Brands { get; set; } = new();

    /// <summary>Pre-formatted headline count shown under the section title.</summary>
    public string TotalBrandsDisplay { get; set; } = string.Empty;

    public string? Search { get; set; }

    public List<DirectoryFilter> Filters { get; set; } = new();

    public List<string> SortOptions { get; set; } = new();

    public string? Sort { get; set; }

    public List<DirectoryStat> Stats { get; set; } = new();

    public string SortOrDefault => Sort ?? SortOptions.FirstOrDefault() ?? string.Empty;
}
