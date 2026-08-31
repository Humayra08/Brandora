using Brandora.Web.Models.Influencers;

namespace Brandora.Web.Models.Discovery;

/// <summary>
/// One creator tile in the "All Influencers" grid.
/// </summary>
public class InfluencerCardViewModel
{
    public string FullName { get; set; } = string.Empty;
    public string Niche { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;

    public int Followers { get; set; }
    public decimal EngagementRate { get; set; }

    public bool Verified { get; set; }

    /// <summary>Facebook / TikTok / Instagram — drives the badge on the card cover.</summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>CSS background for the card's cover strip.</summary>
    public string CoverBackground { get; set; } = "#101733";

    public string? AvatarImageUrl { get; set; }

    public string ProfileUrl { get; set; } = "#";

    public bool HasAvatarImage => !string.IsNullOrWhiteSpace(AvatarImageUrl);

    /// <summary>Falls back to initials, matching the existing creator-list convention.</summary>
    public string Initials => FollowerFormat.InitialsOf(FullName);

    public string FollowersDisplay => FollowerFormat.Format(Followers);

    public string EngagementDisplay => EngagementRate.ToString("0.0") + "%";
}

/// <summary>
/// Backing model for the public "For Influencers" directory page.
/// Populated from a static source today; swapping in a repository later
/// should not require changes to the Razor view.
/// </summary>
public class InfluencerDirectoryViewModel
{
    public List<InfluencerCardViewModel> Influencers { get; set; } = new();

    /// <summary>Pre-formatted headline count shown under the section title.</summary>
    public string TotalInfluencersDisplay { get; set; } = string.Empty;

    public string? Search { get; set; }

    public List<DirectoryFilter> Filters { get; set; } = new();

    public List<string> SortOptions { get; set; } = new();

    public string? Sort { get; set; }

    public List<DirectoryStat> Stats { get; set; } = new();

    public string SortOrDefault => Sort ?? SortOptions.FirstOrDefault() ?? string.Empty;
}
