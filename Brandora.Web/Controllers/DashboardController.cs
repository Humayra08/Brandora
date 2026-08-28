using Brandora.Web.Data;
using Brandora.Web.Models.Dashboard;
using Brandora.Web.Models.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Controllers;

[Authorize]
public class DashboardController(UserManager<ApplicationUser> userManager, ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var userId = userManager.GetUserId(User);

        var brand = await db.BrandProfiles
            .Include(b => b.Campaigns)
            .FirstOrDefaultAsync(b => b.UserId == userId);

        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var pendingProposalCount = await db.Proposals
            .Where(p => p.Campaign.BrandProfileId == brand.Id
                        && p.Status == ProposalStatus.Pending
                        && p.InitiatedBy == ProposalInitiator.Influencer)
            .CountAsync();

        var vm = new DashboardViewModel
        {
            CompanyName = brand.CompanyName,
            DraftCount = brand.Campaigns.Count(c => c.Status == CampaignStatus.Draft),
            PublishedCount = brand.Campaigns.Count(c => c.Status == CampaignStatus.Published),
            ActiveCount = brand.Campaigns.Count(c => c.Status == CampaignStatus.Active),
            CompletedCount = brand.Campaigns.Count(c => c.Status == CampaignStatus.Completed),
            TotalBudget = brand.Campaigns.Sum(c => c.Budget),
            TotalSpend = brand.Campaigns.Sum(c => c.SpentAmount),
            PendingProposalCount = pendingProposalCount,
            RecentCampaigns = brand.Campaigns.OrderByDescending(c => c.CreatedAt).Take(5).ToList()
        };

        return View(vm);
    }
}
