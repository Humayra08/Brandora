using Brandora.Web.Data;
using Brandora.Web.Models.Dashboard;
using Brandora.Web.Models.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Brandora.Web.Controllers;

public class SocialAccountsController(UserManager<ApplicationUser> userManager, ApplicationDbContext db) : InfluencerControllerBase(userManager, db)
{
    public async Task<IActionResult> Index()
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var handle = string.IsNullOrWhiteSpace(influencer.PlatformUsername)
            ? "@" + influencer.FullName.Split(' ')[0].ToLowerInvariant()
            : influencer.PlatformUsername;

        var primaryFollowers = influencer.Followers > 0 ? influencer.Followers : 25400;

        var platforms = new List<ConnectedPlatformRow>
        {
            new() { Name = "Instagram", Icon = "bi-instagram", IconGradient = "linear-gradient(135deg,#f9ce34,#ee2a7b,#6228d7)", Handle = handle, Verified = true, Followers = primaryFollowers, FollowerLabel = "Followers", Connected = true },
            new() { Name = "TikTok", Icon = "bi-tiktok", IconGradient = "linear-gradient(135deg,#111112,#2b2b2e)", Handle = handle, Verified = true, Followers = (int)(primaryFollowers * 0.74), FollowerLabel = "Followers", Connected = true },
            new() { Name = "YouTube", Icon = "bi-youtube", IconGradient = "linear-gradient(135deg,#ff4b4b,#d40f0f)", Handle = handle.TrimStart('@'), Verified = true, Followers = (int)(primaryFollowers * 0.48), FollowerLabel = "Subscribers", Connected = true },
            new() { Name = "Facebook", Icon = "bi-facebook", IconGradient = "linear-gradient(135deg,#3f8cff,#1857d6)", Handle = influencer.FullName, Verified = true, Followers = (int)(primaryFollowers * 0.33), FollowerLabel = "Followers", Connected = true },
        };

        var totalFollowers = platforms.Sum(p => p.Followers);

        var vm = new SocialAccountsViewModel
        {
            Profile = influencer,
            Platforms = platforms,
            TotalFollowers = totalFollowers,
            TotalFollowersGrowthPercent = 12,
            TotalReach = totalFollowers * 5,
            TotalReachGrowthPercent = 16,
            EngagementRate = influencer.EngagementRate > 0 ? influencer.EngagementRate : 6.8m,
            EngagementRateGrowthPercent = 8,
            AvgViews = (int)(totalFollowers * 0.75),
            AvgViewsGrowthPercent = 9,
            ReachSeries = new List<ReachPoint>
            {
                new() { Label = "Apr 1", Value = totalFollowers * 0.62m },
                new() { Label = "Apr 8", Value = totalFollowers * 0.7m },
                new() { Label = "Apr 15", Value = totalFollowers * 0.66m },
                new() { Label = "Apr 22", Value = totalFollowers * 0.88m },
                new() { Label = "Apr 29", Value = totalFollowers * 1.0m },
            }
        };

        return View(vm);
    }
}
