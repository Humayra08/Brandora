using Brandora.Web.Models.Domain;
using Brandora.Web.Models.Influencers;

namespace Brandora.Web.Models.Discovery;

/// <summary>
/// Who is looking at a public profile, so its calls to action fit them:
/// signed-out visitors are invited to join, creators are sent to apply, brands to invite.
/// </summary>
public enum PublicViewer
{
    Visitor,
    Brand,
    Influencer
}

/// <summary>A live social post delivered through Brandora (an approved milestone's post link).</summary>
public class PublicLivePost
{
    public string Url { get; set; } = string.Empty;
    public string CreatorName { get; set; } = string.Empty;
    public int? CreatorId { get; set; }
    public string BrandName { get; set; } = string.Empty;
    public int? BrandId { get; set; }
    public string CampaignTitle { get; set; } = string.Empty;
    public string MilestoneTitle { get; set; } = string.Empty;
    public DateTime Date { get; set; }

    public SocialPlatform? Platform => SocialPlatform.FromUrl(Url);
    public string Host => Uri.TryCreate(Url, UriKind.Absolute, out var uri) ? uri.Host.Replace("www.", "") : Url;
}

/// <summary>One campaign on a brand's public profile. Money is never shown publicly.</summary>
public class PublicCampaignCard
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ContentGuidelines { get; set; }
    public CampaignStatus Status { get; set; }
    public string? Platform { get; set; }
    public string? Niche { get; set; }
    public string? TargetLocation { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? Deadline { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? BannerUrl { get; set; }
    public string? SocialPostUrl { get; set; }
    public int CreatorCount { get; set; }

    /// <summary>Open for applications: published or running, and not past its deadline.</summary>
    public bool IsOpen => Status is CampaignStatus.Published or CampaignStatus.Active &&
                          (!Deadline.HasValue || Deadline.Value.Date >= DateTime.UtcNow.Date);

    public (string Label, string Css) Badge => Status switch
    {
        CampaignStatus.Completed => ("Completed", "is-done"),
        _ when IsOpen && Status == CampaignStatus.Active => ("Live now", "is-live"),
        _ when IsOpen => ("Open to creators", "is-open"),
        _ => ("Closed", "is-closed")
    };

    public SocialPlatform? LivePlatform => SocialPlatform.FromUrl(SocialPostUrl);
}

/// <summary>A creator shown on a brand's public profile (only admin-verified creators).</summary>
public class PublicCreatorCard
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Niche { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public int Followers { get; set; }
    public decimal EngagementRate { get; set; }
    public int Collaborations { get; set; }
    public string CoverBackground { get; set; } = "#101733";

    public string Initials => FollowerFormat.InitialsOf(FullName);
    public string FollowersDisplay => FollowerFormat.Format(Followers);
}

/// <summary>A small brand tile (similar brands, brands a creator worked with).</summary>
public class PublicBrandTile
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string LogoBackground { get; set; } = "#1c2340";
    public int OpenCampaigns { get; set; }

    public string Initials => FollowerFormat.InitialsOf(CompanyName);
}

public class PublicBrandProfileViewModel
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string LogoBackground { get; set; } = "#1c2340";
    public string? WebsiteUrl { get; set; }
    public DateTime MemberSince { get; set; }
    public List<(SocialPlatform Platform, string Url)> SocialLinks { get; set; } = new();

    public List<PublicCampaignCard> Campaigns { get; set; } = new();
    public List<PublicCreatorCard> Creators { get; set; } = new();
    public List<PublicLivePost> LivePosts { get; set; } = new();
    /// <summary>All verified live posts; <see cref="LivePosts"/> holds the most recent ones.</summary>
    public int LivePostTotal { get; set; }
    public List<PublicBrandTile> SimilarBrands { get; set; } = new();

    public int OpenCampaignCount => Campaigns.Count(c => c.IsOpen);
    public int CompletedCampaignCount => Campaigns.Count(c => c.Status == CampaignStatus.Completed);
    public int CollaborationCount { get; set; }

    public PublicViewer Viewer { get; set; }

    public string Initials => FollowerFormat.InitialsOf(CompanyName);
    public string? WebsiteHost => Uri.TryCreate(WebsiteUrl, UriKind.Absolute, out var uri) ? uri.Host.Replace("www.", "") : null;
}

/// <summary>One collaboration on a creator's public profile (only with admin-verified brands).</summary>
public class PublicCollaborationCard
{
    public int CampaignId { get; set; }
    public string CampaignTitle { get; set; } = string.Empty;
    public string? CampaignBannerUrl { get; set; }
    public string? Platform { get; set; }
    public string? Niche { get; set; }
    public CollaborationStatus Status { get; set; }
    public DateTime Since { get; set; }
    public int MilestonesDelivered { get; set; }
    public int MilestonesTotal { get; set; }
    public PublicBrandTile Brand { get; set; } = new();
    public List<string> LivePostUrls { get; set; } = new();

    public int ProgressPercent => MilestonesTotal == 0 ? 0 : (int)Math.Round(MilestonesDelivered * 100.0 / MilestonesTotal);
}

public class PublicInfluencerProfileViewModel
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Niche { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public string CoverBackground { get; set; } = "#101733";
    public string PrimaryPlatform { get; set; } = string.Empty;
    public string PlatformUsername { get; set; } = string.Empty;
    public string AudienceSize { get; set; } = string.Empty;
    public string? WebsiteUrl { get; set; }
    public int Followers { get; set; }
    public decimal EngagementRate { get; set; }
    public DateTime MemberSince { get; set; }

    public List<PublicCollaborationCard> Collaborations { get; set; } = new();
    public List<PublicLivePost> LivePosts { get; set; } = new();
    /// <summary>All verified live posts; <see cref="LivePosts"/> holds the most recent ones.</summary>
    public int LivePostTotal { get; set; }
    public List<PublicCreatorCard> SimilarCreators { get; set; } = new();

    public int CompletedCollaborationCount => Collaborations.Count(c => c.Status == CollaborationStatus.Completed);
    public int BrandCount => Collaborations.Select(c => c.Brand.Id).Distinct().Count();

    public PublicViewer Viewer { get; set; }

    public string Initials => FollowerFormat.InitialsOf(FullName);
    public string FollowersDisplay => FollowerFormat.Format(Followers);
    public string? WebsiteHost => Uri.TryCreate(WebsiteUrl, UriKind.Absolute, out var uri) ? uri.Host.Replace("www.", "") : null;

    /// <summary>The creator's handle as entered, shown with a leading @ unless it's already a link.</summary>
    public string HandleDisplay
    {
        get
        {
            var handle = PlatformUsername.Trim();
            if (handle.Length == 0 || handle.Contains('/')) return string.Empty;
            return handle.StartsWith('@') ? handle : "@" + handle;
        }
    }
}
