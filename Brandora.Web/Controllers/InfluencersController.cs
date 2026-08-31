using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Brandora.Web.Models.Influencers;
using Brandora.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Controllers;

public class InfluencersController(UserManager<ApplicationUser> userManager, ApplicationDbContext db) : BrandControllerBase(userManager, db)
{
    public async Task<IActionResult> Index(string? search, string? niche, string? platform, string? sort)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var query = db.InfluencerProfiles.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(i => i.FullName.Contains(search) || i.PlatformUsername.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(niche))
        {
            query = query.Where(i => i.ContentNiche == niche);
        }

        if (!string.IsNullOrWhiteSpace(platform))
        {
            query = query.Where(i => i.PrimaryPlatform == platform);
        }

        query = sort switch
        {
            "followers" => query.OrderByDescending(i => i.Followers),
            "engagement" => query.OrderByDescending(i => i.EngagementRate),
            _ => query.OrderByDescending(i => i.CreatedAt)
        };

        var creators = await query.ToListAsync();

        var shortlistedIds = await db.ShortlistEntries
            .Where(s => s.BrandProfileId == brand.Id)
            .Select(s => s.InfluencerProfileId)
            .ToListAsync();

        var vm = new CreatorListViewModel
        {
            Creators = creators,
            ShortlistedIds = shortlistedIds.ToHashSet(),
            Search = search,
            Niche = niche,
            Platform = platform,
            Sort = sort,
            TotalCount = await db.InfluencerProfiles.CountAsync()
        };

        return View(vm);
    }

    public async Task<IActionResult> Profile(int id)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var creator = await db.InfluencerProfiles.FirstOrDefaultAsync(i => i.Id == id);
        if (creator is null)
        {
            return NotFound();
        }

        var isShortlisted = await db.ShortlistEntries
            .AnyAsync(s => s.BrandProfileId == brand.Id && s.InfluencerProfileId == id);

        return View(new CreatorProfileViewModel { Creator = creator, IsShortlisted = isShortlisted });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleShortlist(int influencerId, string? returnUrl)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var existing = await db.ShortlistEntries
            .FirstOrDefaultAsync(s => s.BrandProfileId == brand.Id && s.InfluencerProfileId == influencerId);

        if (existing is not null)
        {
            db.ShortlistEntries.Remove(existing);
        }
        else
        {
            var creatorExists = await db.InfluencerProfiles.AnyAsync(i => i.Id == influencerId);
            if (!creatorExists)
            {
                return NotFound();
            }

            db.ShortlistEntries.Add(new ShortlistEntry
            {
                BrandProfileId = brand.Id,
                InfluencerProfileId = influencerId
            });
        }

        await db.SaveChangesAsync();

        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction("Index");
    }

    public async Task<IActionResult> Shortlist()
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var creators = await db.ShortlistEntries
            .Where(s => s.BrandProfileId == brand.Id)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => s.InfluencerProfile)
            .ToListAsync();

        return View(creators);
    }

    public async Task<IActionResult> SmartMatch(int campaignId)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.Id == campaignId && c.BrandProfileId == brand.Id);
        if (campaign is null)
        {
            return NotFound();
        }

        var creators = await db.InfluencerProfiles.ToListAsync();

        var shortlistedIds = (await db.ShortlistEntries
            .Where(s => s.BrandProfileId == brand.Id)
            .Select(s => s.InfluencerProfileId)
            .ToListAsync()).ToHashSet();

        var results = creators
            .Select(c =>
            {
                var (score, reasons) = SmartMatchScorer.ComputeMatch(c, campaign);
                return new SmartMatchResult
                {
                    Creator = c,
                    Score = score,
                    Reasons = reasons,
                    IsShortlisted = shortlistedIds.Contains(c.Id)
                };
            })
            .OrderByDescending(r => r.Score)
            .ToList();

        return View(new SmartMatchViewModel { Campaign = campaign, Results = results });
    }
}
