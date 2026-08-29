using Brandora.Web.Data;
using Brandora.Web.Models.Dashboard;
using Brandora.Web.Models.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Controllers;

public class InfluencerDashboardController(UserManager<ApplicationUser> userManager, ApplicationDbContext db) : InfluencerControllerBase(userManager, db)
{
    public async Task<IActionResult> Index()
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var collaborations = await db.Collaborations
            .Where(c => c.InfluencerProfileId == influencer.Id)
            .Include(c => c.Campaign).ThenInclude(camp => camp.BrandProfile)
            .Include(c => c.Milestones)
            .ToListAsync();

        var payments = await db.Payments
            .Where(p => p.Collaboration.InfluencerProfileId == influencer.Id)
            .Include(p => p.Collaboration)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1);

        var activeCampaigns = collaborations
            .Where(c => c.Status == CollaborationStatus.Active)
            .OrderByDescending(c => c.CreatedAt)
            .Take(5)
            .Select(c =>
            {
                var total = c.Milestones.Count;
                var completed = c.Milestones.Count(m => m.Status is MilestoneStatus.Approved or MilestoneStatus.Paid);

                return new ActiveCampaignRow
                {
                    CollaborationId = c.Id,
                    CampaignTitle = c.Campaign.Title,
                    BrandName = c.Campaign.BrandProfile.CompanyName,
                    Platform = c.Campaign.Platform,
                    TotalValue = c.Milestones.Sum(m => m.Amount),
                    ProgressPercent = total == 0 ? 0 : (int)Math.Round(completed * 100.0 / total),
                    CompletedMilestones = Math.Min(completed + (completed < total ? 1 : 0), total),
                    TotalMilestones = total
                };
            })
            .ToList();

        var upcomingMilestones = collaborations
            .SelectMany(c => c.Milestones.Select(m => new { Collaboration = c, Milestone = m }))
            .Where(x => x.Milestone.Status is MilestoneStatus.Pending or MilestoneStatus.Submitted)
            .OrderBy(x => x.Milestone.DueDate ?? DateTime.MaxValue)
            .Take(5)
            .Select(x => new UpcomingMilestoneRow
            {
                MilestoneId = x.Milestone.Id,
                CampaignTitle = x.Collaboration.Campaign.Title,
                BrandName = x.Collaboration.Campaign.BrandProfile.CompanyName,
                MilestoneTitle = x.Milestone.Title,
                Amount = x.Milestone.Amount,
                DueDate = x.Milestone.DueDate,
                Status = x.Milestone.Status
            })
            .ToList();

        var monthPayments = payments.Where(p => (p.PaidAt ?? p.CreatedAt) >= monthStart).ToList();

        var series = new List<EarningsPoint>();
        var weekStart = monthStart;
        while (weekStart <= now)
        {
            var weekEnd = weekStart.AddDays(7);
            var amount = monthPayments
                .Where(p => (p.PaidAt ?? p.CreatedAt) >= weekStart && (p.PaidAt ?? p.CreatedAt) < weekEnd)
                .Sum(p => p.Amount);

            series.Add(new EarningsPoint { Label = weekStart.ToString("MMM d"), Amount = amount });
            weekStart = weekEnd;
        }

        if (series.Count == 0)
        {
            series.Add(new EarningsPoint { Label = monthStart.ToString("MMM d"), Amount = 0 });
        }

        var profileFields = new[]
        {
            !string.IsNullOrWhiteSpace(influencer.FullName),
            !string.IsNullOrWhiteSpace(influencer.Bio),
            !string.IsNullOrWhiteSpace(influencer.Location),
            !string.IsNullOrWhiteSpace(influencer.PrimaryPlatform),
            !string.IsNullOrWhiteSpace(influencer.PlatformUsername),
            !string.IsNullOrWhiteSpace(influencer.ContentNiche),
            !string.IsNullOrWhiteSpace(influencer.AudienceSize),
            influencer.Followers > 0,
            influencer.EngagementRate > 0,
            influencer.Verified
        };

        var vm = new InfluencerDashboardViewModel
        {
            Profile = influencer,
            ActiveCampaignCount = collaborations.Count(c => c.Status == CollaborationStatus.Active),
            CompletedCampaignCount = collaborations.Count(c => c.Status == CollaborationStatus.Completed),
            ProfileStrengthPercent = (int)Math.Round(profileFields.Count(f => f) * 100.0 / profileFields.Length),
            PendingEarnings = payments.Where(p => p.Status == PaymentStatus.Pending).Sum(p => p.Amount),
            TotalEarnings = payments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount),
            MonthPaid = monthPayments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount),
            MonthPending = monthPayments.Where(p => p.Status == PaymentStatus.Pending).Sum(p => p.Amount),
            ActiveCampaigns = activeCampaigns,
            UpcomingMilestones = upcomingMilestones,
            EarningsSeries = series
        };

        return View(vm);
    }
}
