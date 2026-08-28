using Brandora.Web.Data;
using Brandora.Web.Models.Dashboard;
using Brandora.Web.Models.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Controllers;

public class DashboardController(UserManager<ApplicationUser> userManager, ApplicationDbContext db) : BrandControllerBase(userManager, db)
{
    public async Task<IActionResult> Index()
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var campaigns = await db.Campaigns
            .Where(c => c.BrandProfileId == brand.Id)
            .ToListAsync();

        var pendingProposalCount = await db.Proposals
            .Where(p => p.Campaign.BrandProfileId == brand.Id
                        && p.Status == ProposalStatus.Pending
                        && p.InitiatedBy == ProposalInitiator.Influencer)
            .CountAsync();

        var vm = new DashboardViewModel
        {
            CompanyName = brand.CompanyName,
            DraftCount = campaigns.Count(c => c.Status == CampaignStatus.Draft),
            PublishedCount = campaigns.Count(c => c.Status == CampaignStatus.Published),
            ActiveCount = campaigns.Count(c => c.Status == CampaignStatus.Active),
            CompletedCount = campaigns.Count(c => c.Status == CampaignStatus.Completed),
            TotalBudget = campaigns.Sum(c => c.Budget),
            TotalSpend = campaigns.Sum(c => c.SpentAmount),
            PendingProposalCount = pendingProposalCount,
            RecentCampaigns = campaigns.OrderByDescending(c => c.CreatedAt).Take(5).ToList()
        };

        return View(vm);
    }
}
