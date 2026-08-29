using Brandora.Web.Data;
using Brandora.Web.Models.Analytics;
using Brandora.Web.Models.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Controllers;

public class AnalyticsController(UserManager<ApplicationUser> userManager, ApplicationDbContext db) : BrandControllerBase(userManager, db)
{
    private static readonly Dictionary<CampaignStatus, string> StatusColors = new()
    {
        [CampaignStatus.Draft] = "#ffb648",
        [CampaignStatus.Published] = "#2694ff",
        [CampaignStatus.Active] = "#12c48b",
        [CampaignStatus.Completed] = "#9b2cff",
        [CampaignStatus.Cancelled] = "#e0507a"
    };

    public async Task<IActionResult> Index()
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var campaigns = await db.Campaigns.Where(c => c.BrandProfileId == brand.Id).ToListAsync();
        var proposals = await db.Proposals.Where(p => p.Campaign.BrandProfileId == brand.Id).ToListAsync();
        var collaborations = await db.Collaborations.Where(c => c.Campaign.BrandProfileId == brand.Id).ToListAsync();

        var payments = await db.Payments
            .Where(p => p.Collaboration.Campaign.BrandProfileId == brand.Id && p.Status == PaymentStatus.Completed)
            .Include(p => p.Collaboration).ThenInclude(c => c.InfluencerProfile)
            .ToListAsync();

        var statusBreakdown = campaigns
            .GroupBy(c => c.Status)
            .Select(g => new CampaignStatusSlice { Status = g.Key.ToString(), Count = g.Count(), Color = StatusColors[g.Key] })
            .OrderByDescending(s => s.Count)
            .ToList();

        var months = Enumerable.Range(0, 6)
            .Select(i => DateTime.UtcNow.AddMonths(-i))
            .Reverse()
            .Select(d => new { d.Year, d.Month, Label = d.ToString("MMM") })
            .ToList();

        var spendByMonth = months.Select(m => new MonthlySpend
        {
            Month = m.Label,
            Amount = payments
                .Where(p => p.PaidAt.HasValue && p.PaidAt.Value.Year == m.Year && p.PaidAt.Value.Month == m.Month)
                .Sum(p => p.Amount)
        }).ToList();

        var topCreators = payments
            .GroupBy(p => p.Collaboration.InfluencerProfile)
            .Select(g => new TopCreator { Name = g.Key.FullName, InfluencerProfileId = g.Key.Id, TotalPaid = g.Sum(p => p.Amount) })
            .OrderByDescending(t => t.TotalPaid)
            .Take(5)
            .ToList();

        var vm = new AnalyticsViewModel
        {
            TotalSpend = campaigns.Sum(c => c.SpentAmount),
            TotalBudget = campaigns.Sum(c => c.Budget),
            TotalCampaigns = campaigns.Count,
            ActiveCollaborations = collaborations.Count(c => c.Status == CollaborationStatus.Active),
            ProposalsSent = proposals.Count,
            ProposalsAccepted = proposals.Count(p => p.Status == ProposalStatus.Accepted),
            ProposalsRejected = proposals.Count(p => p.Status == ProposalStatus.Rejected),
            ProposalsPending = proposals.Count(p => p.Status == ProposalStatus.Pending),
            StatusBreakdown = statusBreakdown,
            SpendByMonth = spendByMonth,
            TopCreators = topCreators
        };

        return View(vm);
    }
}
