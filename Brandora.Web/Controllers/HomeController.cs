using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Brandora.Web.Data;
using Brandora.Web.Models;
using Brandora.Web.Models.Discovery;
using Brandora.Web.Models.Contact;
using Brandora.Web.Models.Domain;

namespace Brandora.Web.Controllers;

public class HomeController(ApplicationDbContext db) : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet("platform")]
    public IActionResult Platform()
    {
        return View();
    }

    // ---------------------------------------------------------------------------------
    // Public directories. Only profiles an admin has approved via User Verification are
    // listed, and only public-safe fields leave the queries (no contact details, budgets,
    // payment amounts or admin data). Every filter, sort and figure comes from the
    // database — nothing on these pages is made up.
    // ---------------------------------------------------------------------------------

    private const string AnyIndustry = "All Industries";
    private const string AnyPlatform = "All Platforms";
    private const string AnyBrandStatus = "All Brands";
    private const string HiringNow = "Hiring Now";
    private const string AnyNiche = "All Niches";
    private const string AnyLocation = "All Locations";
    private const string AnyFollowers = "Any";

    private static readonly string[] PublicPlatforms = { "Facebook", "Instagram", "TikTok", "YouTube" };

    private static readonly (string Label, int Min, int Max)[] FollowerRanges =
    {
        ("Under 10K", 0, 9_999),
        ("10K - 100K", 10_000, 99_999),
        ("100K - 500K", 100_000, 499_999),
        ("500K - 1M", 500_000, 999_999),
        ("1M+", 1_000_000, int.MaxValue)
    };

    [HttpGet("for-brands")]
    public async Task<IActionResult> ForBrands(string? search, string? industry, string? platform, string? status, string? sort)
    {
        var verified = db.BrandProfiles.AsNoTracking().Where(b => b.VerificationStatus == VerificationStatus.Verified);
        var today = DateTime.UtcNow.Date;

        var query = verified;
        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            var lowered = term.ToLower();
            query = query.Where(b => b.CompanyName.ToLower().Contains(lowered) || b.Industry.ToLower().Contains(lowered));
        }

        if (!string.IsNullOrWhiteSpace(industry) && industry != AnyIndustry)
        {
            query = query.Where(b => b.Industry == industry);
        }

        if (!string.IsNullOrWhiteSpace(platform) && platform != AnyPlatform)
        {
            var loweredPlatform = platform.ToLower();
            query = query.Where(b => b.Campaigns.Any(c =>
                c.Status != CampaignStatus.Draft && c.Status != CampaignStatus.Cancelled &&
                c.Platform != null && c.Platform.ToLower().Contains(loweredPlatform)));
        }

        if (status == HiringNow)
        {
            query = query.Where(b => b.Campaigns.Any(c =>
                (c.Status == CampaignStatus.Published || c.Status == CampaignStatus.Active) &&
                (c.Deadline == null || c.Deadline >= today)));
        }

        var rows = await query
            .Select(b => new
            {
                b.Id,
                b.CompanyName,
                b.Industry,
                b.ProfilePictureUrl,
                b.CreatedAt,
                OpenCampaigns = b.Campaigns.Count(c =>
                    (c.Status == CampaignStatus.Published || c.Status == CampaignStatus.Active) &&
                    (c.Deadline == null || c.Deadline >= today)),
                LatestOpenCampaign = b.Campaigns
                    .Where(c => (c.Status == CampaignStatus.Published || c.Status == CampaignStatus.Active) &&
                                (c.Deadline == null || c.Deadline >= today))
                    .OrderByDescending(c => c.CreatedAt)
                    .Select(c => c.Title)
                    .FirstOrDefault()
            })
            .ToListAsync();

        var vm = DirectoryData.BuildBrandDirectory();
        var sortChoice = vm.SortOptions.Contains(sort ?? "") ? sort! : vm.SortOptions[0];
        rows = sortChoice switch
        {
            "Oldest First" => rows.OrderBy(b => b.CreatedAt).ToList(),
            "Most Campaigns" => rows.OrderByDescending(b => b.OpenCampaigns).ThenBy(b => b.CompanyName).ToList(),
            "Name (A-Z)" => rows.OrderBy(b => b.CompanyName, StringComparer.OrdinalIgnoreCase).ToList(),
            _ => rows.OrderByDescending(b => b.CreatedAt).ToList()
        };

        var industries = await verified.Select(b => b.Industry).Where(i => i != "").Distinct().ToListAsync();

        vm.Search = term;
        vm.Sort = sortChoice;
        vm.Filters = new List<DirectoryFilter>
        {
            new() { Label = "Industry", Name = "industry", Options = new[] { AnyIndustry }.Concat(industries.OrderBy(i => i)).ToList(), Selected = industry },
            new() { Label = "Campaign Platform", Name = "platform", Options = new[] { AnyPlatform }.Concat(PublicPlatforms).ToList(), Selected = platform },
            new() { Label = "Availability", Name = "status", Options = new List<string> { AnyBrandStatus, HiringNow }, Selected = status }
        };
        vm.TotalBrandsDisplay = rows.Count.ToString("N0");
        vm.Brands = rows.Select(b => new BrandCardViewModel
        {
            CompanyName = b.CompanyName,
            Industry = b.Industry,
            ActiveCampaigns = b.OpenCampaigns,
            Description = b.LatestOpenCampaign is null ? string.Empty : $"Now hiring: {b.LatestOpenCampaign}",
            LogoText = b.CompanyName,
            LogoImageUrl = b.ProfilePictureUrl,
            LogoBackground = DirectoryData.BrandLogoBackgrounds[b.Id % DirectoryData.BrandLogoBackgrounds.Length],
            ProfileUrl = $"/for-brands/{b.Id}"
        }).ToList();
        vm.Stats = await BuildPlatformStatsAsync(forBrands: true);

        return View(vm);
    }

    [HttpGet("for-influencers")]
    public async Task<IActionResult> ForInfluencers(string? search, string? platform, string? niche, string? location, string? followers, string? sort)
    {
        var verified = db.InfluencerProfiles.AsNoTracking().Where(i => i.VerificationStatus == VerificationStatus.Verified);

        var query = verified;
        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            var lowered = term.ToLower();
            query = query.Where(i => i.FullName.ToLower().Contains(lowered) ||
                                     i.ContentNiche.ToLower().Contains(lowered) ||
                                     (i.Location != null && i.Location.ToLower().Contains(lowered)));
        }

        if (!string.IsNullOrWhiteSpace(platform) && platform != AnyPlatform)
        {
            var loweredPlatform = platform.ToLower();
            query = query.Where(i => i.PrimaryPlatform.ToLower().Contains(loweredPlatform));
        }

        if (!string.IsNullOrWhiteSpace(niche) && niche != AnyNiche)
        {
            query = query.Where(i => i.ContentNiche == niche);
        }

        if (!string.IsNullOrWhiteSpace(location) && location != AnyLocation)
        {
            query = query.Where(i => i.Location == location);
        }

        var range = FollowerRanges.FirstOrDefault(r => r.Label == followers);
        if (range.Label is not null)
        {
            var minFollowers = range.Min;
            var maxFollowers = range.Max;
            query = query.Where(i => i.Followers >= minFollowers && i.Followers <= maxFollowers);
        }

        var rows = await query
            .Select(i => new
            {
                i.Id,
                i.FullName,
                i.ContentNiche,
                i.Location,
                i.Followers,
                i.EngagementRate,
                i.PrimaryPlatform,
                i.ProfilePictureUrl,
                i.CreatedAt
            })
            .ToListAsync();

        var vm = DirectoryData.BuildInfluencerDirectory();
        var sortChoice = vm.SortOptions.Contains(sort ?? "") ? sort! : vm.SortOptions[0];
        rows = sortChoice switch
        {
            "Followers: Low to High" => rows.OrderBy(i => i.Followers).ToList(),
            "Engagement: High to Low" => rows.OrderByDescending(i => i.EngagementRate).ToList(),
            "Newest First" => rows.OrderByDescending(i => i.CreatedAt).ToList(),
            _ => rows.OrderByDescending(i => i.Followers).ToList()
        };

        var niches = await verified.Select(i => i.ContentNiche).Where(n => n != "").Distinct().ToListAsync();
        var locations = await verified.Where(i => i.Location != null && i.Location != "").Select(i => i.Location!).Distinct().ToListAsync();

        vm.Search = term;
        vm.Sort = sortChoice;
        vm.Filters = new List<DirectoryFilter>
        {
            new() { Label = "Platform", Name = "platform", Options = new[] { AnyPlatform }.Concat(PublicPlatforms).ToList(), Selected = platform },
            new() { Label = "Niche", Name = "niche", Options = new[] { AnyNiche }.Concat(niches.OrderBy(n => n)).ToList(), Selected = niche },
            new() { Label = "Location", Name = "location", Options = new[] { AnyLocation }.Concat(locations.OrderBy(l => l)).ToList(), Selected = location },
            new() { Label = "Followers", Name = "followers", Options = new[] { AnyFollowers }.Concat(FollowerRanges.Select(r => r.Label)).ToList(), Selected = followers }
        };
        vm.TotalInfluencersDisplay = rows.Count.ToString("N0");
        vm.Influencers = rows.Select(i => new InfluencerCardViewModel
        {
            FullName = i.FullName,
            Niche = i.ContentNiche,
            Location = i.Location ?? string.Empty,
            Followers = i.Followers,
            EngagementRate = i.EngagementRate,
            Verified = true,
            Platform = i.PrimaryPlatform,
            AvatarImageUrl = i.ProfilePictureUrl,
            CoverBackground = DirectoryData.InfluencerCoverBackgrounds[i.Id % DirectoryData.InfluencerCoverBackgrounds.Length],
            ProfileUrl = $"/for-influencers/{i.Id}"
        }).ToList();
        vm.Stats = await BuildPlatformStatsAsync(forBrands: false);

        return View(vm);
    }

    // Real platform-wide figures for the stats panel under each directory.
    private async Task<List<DirectoryStat>> BuildPlatformStatsAsync(bool forBrands)
    {
        var verifiedInfluencers = await db.InfluencerProfiles.CountAsync(i => i.VerificationStatus == VerificationStatus.Verified);

        if (forBrands)
        {
            var today = DateTime.UtcNow.Date;
            var verifiedBrands = await db.BrandProfiles.CountAsync(b => b.VerificationStatus == VerificationStatus.Verified);
            var openCampaigns = await db.Campaigns.CountAsync(c =>
                c.BrandProfile.VerificationStatus == VerificationStatus.Verified &&
                (c.Status == CampaignStatus.Published || c.Status == CampaignStatus.Active) &&
                (c.Deadline == null || c.Deadline >= today));
            var completedCampaigns = await db.Campaigns.CountAsync(c => c.Status == CampaignStatus.Completed);

            return new List<DirectoryStat>
            {
                new() { Value = verifiedBrands.ToString("N0"), Label = "Verified Brands" },
                new() { Value = openCampaigns.ToString("N0"), Label = "Open Campaigns" },
                new() { Value = completedCampaigns.ToString("N0"), Label = "Campaigns Completed" },
                new() { Value = verifiedInfluencers.ToString("N0"), Label = "Verified Creators" }
            };
        }

        var activeCollaborations = await db.Collaborations.CountAsync(c => c.Status == CollaborationStatus.Active);
        var reach = await db.InfluencerProfiles
            .Where(i => i.VerificationStatus == VerificationStatus.Verified)
            .SumAsync(i => (long)i.Followers);
        var postsDelivered = await db.Milestones.CountAsync(m => m.Status == MilestoneStatus.Approved || m.Status == MilestoneStatus.Paid);

        return new List<DirectoryStat>
        {
            new() { Value = verifiedInfluencers.ToString("N0"), Label = "Verified Influencers", Icon = "users" },
            new() { Value = activeCollaborations.ToString("N0"), Label = "Active Collaborations", Icon = "handshake" },
            new() { Value = CompactNumber(reach), Label = "Combined Reach", Icon = "chart" },
            new() { Value = postsDelivered.ToString("N0"), Label = "Verified Posts Delivered", Icon = "shield" }
        };
    }

    private static string CompactNumber(decimal value) => value switch
    {
        >= 1_000_000_000m => (value / 1_000_000_000m).ToString("0.#") + "B",
        >= 1_000_000m => (value / 1_000_000m).ToString("0.#") + "M",
        >= 1_000m => (value / 1_000m).ToString("0.#") + "K",
        _ => value.ToString("0")
    };

    private async Task<PublicViewer> GetViewerAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return PublicViewer.Visitor;
        }

        var role = await db.Users.Where(u => u.Id == userId).Select(u => (UserRole?)u.Role).FirstOrDefaultAsync();
        return role switch
        {
            UserRole.Brand => PublicViewer.Brand,
            UserRole.Influencer => PublicViewer.Influencer,
            _ => PublicViewer.Visitor
        };
    }

    // ---------------------------------------------------------------------------------
    // Public profile pages (reached from the directory cards).
    // ---------------------------------------------------------------------------------

    [HttpGet("for-brands/{id:int}")]
    public async Task<IActionResult> BrandDetails(int id)
    {
        var brand = await db.BrandProfiles.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id && b.VerificationStatus == VerificationStatus.Verified);
        if (brand is null)
        {
            return NotFound();
        }

        var campaigns = await db.Campaigns.AsNoTracking()
            .Where(c => c.BrandProfileId == id && c.Status != CampaignStatus.Draft && c.Status != CampaignStatus.Cancelled)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new PublicCampaignCard
            {
                Id = c.Id,
                Title = c.Title,
                Description = c.Description,
                ContentGuidelines = c.ContentGuidelines,
                Status = c.Status,
                Platform = c.Platform,
                Niche = c.Niche,
                TargetLocation = c.TargetLocation,
                StartDate = c.StartDate,
                Deadline = c.Deadline,
                CreatedAt = c.CreatedAt,
                BannerUrl = c.MediaType == "video" ? null : c.MediaUrl,
                SocialPostUrl = c.SocialPostUrl,
                CreatorCount = db.Collaborations.Count(x => x.CampaignId == c.Id && x.Status != CollaborationStatus.Cancelled)
            })
            .ToListAsync();

        // Keep only live-post links that are real web addresses before they reach the page.
        foreach (var campaign in campaigns.Where(c => !IsWebLink(c.SocialPostUrl)))
        {
            campaign.SocialPostUrl = null;
        }

        var collaborations = await db.Collaborations.AsNoTracking()
            .Where(c => c.Campaign.BrandProfileId == id && c.Status != CollaborationStatus.Cancelled)
            .Include(c => c.InfluencerProfile)
            .ToListAsync();

        var creators = collaborations
            .Where(c => c.InfluencerProfile.VerificationStatus == VerificationStatus.Verified)
            .GroupBy(c => c.InfluencerProfileId)
            .Select(g => ToCreatorCard(g.First().InfluencerProfile, g.Count()))
            .OrderByDescending(c => c.Collaborations).ThenByDescending(c => c.Followers)
            .ToList();

        var (livePosts, livePostTotal) = await LoadLivePostsAsync(m => m.Collaboration.Campaign.BrandProfileId == id);

        var similar = await db.BrandProfiles.AsNoTracking()
            .Where(b => b.VerificationStatus == VerificationStatus.Verified && b.Id != id)
            .OrderByDescending(b => b.Industry == brand.Industry)
            .ThenByDescending(b => b.CreatedAt)
            .Take(3)
            .Select(b => new { b.Id, b.CompanyName, b.Industry, b.ProfilePictureUrl })
            .ToListAsync();
        var similarIds = similar.Select(b => b.Id).ToList();
        var openCounts = await OpenCampaignCountsAsync(similarIds);

        return View(new PublicBrandProfileViewModel
        {
            Id = brand.Id,
            CompanyName = brand.CompanyName,
            Industry = brand.Industry,
            LogoUrl = brand.ProfilePictureUrl,
            LogoBackground = DirectoryData.BrandLogoBackgrounds[brand.Id % DirectoryData.BrandLogoBackgrounds.Length],
            WebsiteUrl = WebLink(brand.WebsiteUrl),
            MemberSince = brand.CreatedAt,
            SocialLinks = brand.SocialLinks().Where(l => IsWebLink(l.Url)).ToList(),
            Campaigns = campaigns,
            Creators = creators,
            CollaborationCount = collaborations.Count,
            LivePosts = livePosts,
            LivePostTotal = livePostTotal,
            SimilarBrands = similar.Select(b => new PublicBrandTile
            {
                Id = b.Id,
                CompanyName = b.CompanyName,
                Industry = b.Industry,
                LogoUrl = b.ProfilePictureUrl,
                LogoBackground = DirectoryData.BrandLogoBackgrounds[b.Id % DirectoryData.BrandLogoBackgrounds.Length],
                OpenCampaigns = openCounts.GetValueOrDefault(b.Id)
            }).ToList(),
            Viewer = await GetViewerAsync()
        });
    }

    [HttpGet("for-influencers/{id:int}")]
    public async Task<IActionResult> InfluencerDetails(int id)
    {
        var influencer = await db.InfluencerProfiles.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id && i.VerificationStatus == VerificationStatus.Verified);
        if (influencer is null)
        {
            return NotFound();
        }

        // Only collaborations with brands that are themselves public (admin-verified).
        var collaborations = await db.Collaborations.AsNoTracking()
            .Where(c => c.InfluencerProfileId == id &&
                        c.Status != CollaborationStatus.Cancelled &&
                        c.Campaign.BrandProfile.VerificationStatus == VerificationStatus.Verified)
            .Include(c => c.Campaign).ThenInclude(c => c.BrandProfile)
            .Include(c => c.Milestones)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        var brandIds = collaborations.Select(c => c.Campaign.BrandProfileId).Distinct().ToList();
        var openCounts = await OpenCampaignCountsAsync(brandIds);

        var cards = collaborations.Select(c => new PublicCollaborationCard
        {
            CampaignId = c.CampaignId,
            CampaignTitle = c.Campaign.Title,
            CampaignBannerUrl = c.Campaign.MediaType == "video" ? null : c.Campaign.MediaUrl,
            Platform = c.Campaign.Platform,
            Niche = c.Campaign.Niche,
            Status = c.Status,
            Since = c.CreatedAt,
            MilestonesTotal = c.Milestones.Count,
            MilestonesDelivered = c.Milestones.Count(m => m.Status is MilestoneStatus.Approved or MilestoneStatus.Paid),
            LivePostUrls = c.Milestones
                .Where(m => m.Status is MilestoneStatus.Approved or MilestoneStatus.Paid)
                .Select(m => m.LivePostUrl())
                .Where(u => u is not null)
                .Select(u => u!)
                .Distinct()
                .ToList(),
            Brand = new PublicBrandTile
            {
                Id = c.Campaign.BrandProfile.Id,
                CompanyName = c.Campaign.BrandProfile.CompanyName,
                Industry = c.Campaign.BrandProfile.Industry,
                LogoUrl = c.Campaign.BrandProfile.ProfilePictureUrl,
                LogoBackground = DirectoryData.BrandLogoBackgrounds[c.Campaign.BrandProfile.Id % DirectoryData.BrandLogoBackgrounds.Length],
                OpenCampaigns = openCounts.GetValueOrDefault(c.Campaign.BrandProfile.Id)
            }
        }).ToList();

        var (livePosts, livePostTotal) = await LoadLivePostsAsync(m => m.Collaboration.InfluencerProfileId == id);

        var similar = await db.InfluencerProfiles.AsNoTracking()
            .Where(i => i.VerificationStatus == VerificationStatus.Verified && i.Id != id)
            .OrderByDescending(i => i.ContentNiche == influencer.ContentNiche)
            .ThenByDescending(i => i.Followers)
            .Take(3)
            .ToListAsync();
        var similarIds = similar.Select(s => s.Id).ToList();
        var similarCollabCounts = await db.Collaborations
            .Where(c => similarIds.Contains(c.InfluencerProfileId) &&
                        c.Status != CollaborationStatus.Cancelled &&
                        c.Campaign.BrandProfile.VerificationStatus == VerificationStatus.Verified)
            .GroupBy(c => c.InfluencerProfileId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count);

        return View(new PublicInfluencerProfileViewModel
        {
            Id = influencer.Id,
            FullName = influencer.FullName,
            Niche = influencer.ContentNiche,
            Location = influencer.Location,
            Bio = influencer.Bio,
            AvatarUrl = influencer.ProfilePictureUrl,
            CoverBackground = DirectoryData.InfluencerCoverBackgrounds[influencer.Id % DirectoryData.InfluencerCoverBackgrounds.Length],
            PrimaryPlatform = influencer.PrimaryPlatform,
            PlatformUsername = influencer.PlatformUsername,
            AudienceSize = influencer.AudienceSize,
            WebsiteUrl = WebLink(influencer.WebsiteUrl),
            Followers = influencer.Followers,
            EngagementRate = influencer.EngagementRate,
            MemberSince = influencer.CreatedAt,
            Collaborations = cards,
            LivePosts = livePosts,
            LivePostTotal = livePostTotal,
            SimilarCreators = similar.Select(s => ToCreatorCard(s, similarCollabCounts.GetValueOrDefault(s.Id))).ToList(),
            Viewer = await GetViewerAsync()
        });
    }

    private static PublicCreatorCard ToCreatorCard(InfluencerProfile i, int collaborations) => new()
    {
        Id = i.Id,
        FullName = i.FullName,
        Niche = i.ContentNiche,
        Platform = i.PrimaryPlatform,
        AvatarUrl = i.ProfilePictureUrl,
        Followers = i.Followers,
        EngagementRate = i.EngagementRate,
        Collaborations = collaborations,
        CoverBackground = DirectoryData.InfluencerCoverBackgrounds[i.Id % DirectoryData.InfluencerCoverBackgrounds.Length]
    };

    private async Task<Dictionary<int, int>> OpenCampaignCountsAsync(List<int> brandIds)
    {
        var today = DateTime.UtcNow.Date;
        return await db.Campaigns
            .Where(c => brandIds.Contains(c.BrandProfileId) &&
                        (c.Status == CampaignStatus.Published || c.Status == CampaignStatus.Active) &&
                        (c.Deadline == null || c.Deadline >= today))
            .GroupBy(c => c.BrandProfileId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count);
    }

    // Live posts are milestones Brandora has verified (Approved or Paid) that carry a link
    // to the post, between a verified brand and a verified creator.
    private const int LivePostLimit = 24;

    private async Task<(List<PublicLivePost> Posts, int Total)> LoadLivePostsAsync(System.Linq.Expressions.Expression<Func<Milestone, bool>> scope)
    {
        var milestones = await db.Milestones.AsNoTracking()
            .Where(scope)
            .Where(m => (m.Status == MilestoneStatus.Approved || m.Status == MilestoneStatus.Paid) &&
                        m.Collaboration.InfluencerProfile.VerificationStatus == VerificationStatus.Verified &&
                        m.Collaboration.Campaign.BrandProfile.VerificationStatus == VerificationStatus.Verified &&
                        (m.ProofPostUrl != null || m.ProofUrl != null))
            .Include(m => m.Collaboration).ThenInclude(c => c.InfluencerProfile)
            .Include(m => m.Collaboration).ThenInclude(c => c.Campaign).ThenInclude(c => c.BrandProfile)
            .OrderByDescending(m => m.BrandApprovedAt ?? m.CreatedAt)
            .ToListAsync();

        var withPosts = milestones
            .Select(m => (Milestone: m, Url: m.LivePostUrl()))
            .Where(x => x.Url is not null)
            .DistinctBy(x => x.Url)
            .ToList();

        var posts = withPosts
            .Take(LivePostLimit)
            .Select(x => new PublicLivePost
            {
                Url = x.Url!,
                CreatorName = x.Milestone.Collaboration.InfluencerProfile.FullName,
                CreatorId = x.Milestone.Collaboration.InfluencerProfileId,
                BrandName = x.Milestone.Collaboration.Campaign.BrandProfile.CompanyName,
                BrandId = x.Milestone.Collaboration.Campaign.BrandProfileId,
                CampaignTitle = x.Milestone.Collaboration.Campaign.Title,
                MilestoneTitle = x.Milestone.Title,
                Date = x.Milestone.BrandApprovedAt ?? x.Milestone.CreatedAt
            })
            .ToList();

        return (posts, withPosts.Count);
    }

    // A website as the profile owner typed it ("mysite.com" or a full URL), as a safe
    // http(s) link — or null when it isn't a usable web address.
    private static string? WebLink(string? value)
    {
        var url = value?.Trim();
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        if (!url.Contains("://", StringComparison.Ordinal))
        {
            url = "https://" + url;
        }

        return IsWebLink(url) && Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Host.Contains('.') ? uri.AbsoluteUri : null;
    }

    private static bool IsWebLink(string? url) =>
        !string.IsNullOrWhiteSpace(url) &&
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    // "Home/Contact" keeps the conventional URL working too — the attribute route above
    // otherwise takes this action out of conventional routing, which is why the
    // in-app "Need Help? → Contact Support" links used to 404. Signed-in pages link
    // here with ?accountType=Brand so the form opens with the right account type.
    [HttpGet("contact")]
    [HttpGet("Home/Contact")]
    public IActionResult Contact(string? accountType)
    {
        var model = new ContactIssueViewModel();
        if (!string.IsNullOrWhiteSpace(accountType) && ContactIssueViewModel.AccountTypes.Contains(accountType))
        {
            model.AccountType = accountType;
        }

        return View(model);
    }

    [HttpPost("contact")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Contact(ContactIssueViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Stored for the admin portal's Contact Submissions page; attachments are
        // still not persisted, so support picks those up over the channels listed
        // alongside the form.
        db.ContactSubmissions.Add(new ContactSubmission
        {
            FullName = model.FullName,
            Email = model.Email,
            AccountType = model.AccountType,
            IssueType = model.IssueType,
            Subject = model.Subject,
            Description = model.Description
        });

        await db.SaveChangesAsync();

        TempData["ContactSubmitted"] = true;

        return RedirectToAction(nameof(Contact));
    }

    // "privacy-policy" matches the hyphenated attribute routes the other public
    // pages use. The second route keeps the original /Home/Privacy URL working,
    // since adding an attribute route would otherwise take it out of conventional
    // routing and break any existing link to it.
    [HttpGet("privacy-policy")]
    [HttpGet("Home/Privacy")]
    public IActionResult Privacy()
    {
        return View();
    }

    // No Terms route existed before this, so there is no legacy URL to preserve —
    // "terms-and-conditions" alone matches the hyphenated convention above.
    [HttpGet("terms-and-conditions")]
    public IActionResult Terms()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
