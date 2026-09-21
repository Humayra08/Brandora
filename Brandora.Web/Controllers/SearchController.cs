using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Brandora.Web.Models.Search;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Controllers;

// Brand-wide global search (topbar). Every result is a real, brand-scoped
// database record — no fabricated results, no client-side arrays. Each
// result links straight to its real detail page.
public class SearchController(UserManager<ApplicationUser> userManager, ApplicationDbContext db) : BrandControllerBase(userManager, db)
{
    private const int MaxPerCategory = 8;

    public async Task<IActionResult> Index(string? q)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        ViewData["Title"] = "Search";
        ViewData["ActiveNav"] = "";

        var vm = new SearchResultsViewModel { Query = q?.Trim() ?? string.Empty };

        if (string.IsNullOrWhiteSpace(vm.Query) || vm.Query.Length < 2)
        {
            return View(vm);
        }

        var term = vm.Query;

        vm.Campaigns = await db.Campaigns
            .Where(c => c.BrandProfileId == brand.Id && c.Title.Contains(term))
            .OrderByDescending(c => c.CreatedAt)
            .Take(MaxPerCategory)
            .ToListAsync();

        vm.Influencers = await db.InfluencerProfiles
            .Where(i => i.FullName.Contains(term) || i.PlatformUsername.Contains(term) || i.ContentNiche.Contains(term))
            .OrderByDescending(i => i.CreatedAt)
            .Take(MaxPerCategory)
            .ToListAsync();

        vm.Applications = await db.Proposals
            .Include(p => p.InfluencerProfile)
            .Include(p => p.Campaign)
            .Where(p => p.Campaign.BrandProfileId == brand.Id &&
                (p.InfluencerProfile.FullName.Contains(term) || p.Campaign.Title.Contains(term)))
            .OrderByDescending(p => p.CreatedAt)
            .Take(MaxPerCategory)
            .ToListAsync();

        vm.Collaborations = await db.Collaborations
            .Include(c => c.InfluencerProfile)
            .Include(c => c.Campaign)
            .Where(c => c.Campaign.BrandProfileId == brand.Id &&
                (c.InfluencerProfile.FullName.Contains(term) || c.Campaign.Title.Contains(term)))
            .OrderByDescending(c => c.CreatedAt)
            .Take(MaxPerCategory)
            .ToListAsync();

        vm.Payments = await db.Payments
            .Include(p => p.Collaboration).ThenInclude(c => c.InfluencerProfile)
            .Include(p => p.Collaboration).ThenInclude(c => c.Campaign)
            .Where(p => p.Collaboration.Campaign.BrandProfileId == brand.Id &&
                (p.Collaboration.InfluencerProfile.FullName.Contains(term) ||
                 p.Collaboration.Campaign.Title.Contains(term) ||
                 (p.TransactionReference != null && p.TransactionReference.Contains(term))))
            .OrderByDescending(p => p.CreatedAt)
            .Take(MaxPerCategory)
            .ToListAsync();

        vm.TotalCount = vm.Campaigns.Count + vm.Influencers.Count + vm.Applications.Count
            + vm.Collaborations.Count + vm.Payments.Count;

        return View(vm);
    }
}
