using Brandora.Web.Data;
using Brandora.Web.Models.Brands;
using Brandora.Web.Models.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Controllers;

public class BrandsController(UserManager<ApplicationUser> userManager, ApplicationDbContext db) : InfluencerControllerBase(userManager, db)
{
    public async Task<IActionResult> Index(string? search, string? industry)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var query = db.BrandProfiles.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(b => b.CompanyName.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(industry))
        {
            query = query.Where(b => b.Industry == industry);
        }

        var brands = await query.OrderByDescending(b => b.CreatedAt).ToListAsync();

        var activeCampaignCounts = await db.Campaigns
            .Where(c => c.Status == CampaignStatus.Published || c.Status == CampaignStatus.Active)
            .GroupBy(c => c.BrandProfileId)
            .Select(g => new { BrandProfileId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.BrandProfileId, g => g.Count);

        var vm = new BrandListViewModel
        {
            Brands = brands,
            InfluencerName = influencer.FullName,
            Notifications = await db.Notifications.AsNoTracking().Where(n => n.UserId == influencer.UserId)
                .OrderByDescending(n => n.CreatedAt).Take(5).ToListAsync(),
            Search = search,
            Industry = industry,
            TotalCount = await db.BrandProfiles.CountAsync(),
            VerifiedCount = await db.BrandProfiles.CountAsync(b => b.VerificationStatus == VerificationStatus.Verified),
            IndustryCount = await db.BrandProfiles.Select(b => b.Industry).Distinct().CountAsync(),
            ActiveCampaignCounts = activeCampaignCounts
        };

        return View(vm);
    }
}
