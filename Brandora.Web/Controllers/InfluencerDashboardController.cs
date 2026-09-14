using Brandora.Web.Data;
using Brandora.Web.Models.Dashboard;
using Brandora.Web.Models.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Brandora.Web.Controllers;

public class InfluencerDashboardController : InfluencerControllerBase
{
    private readonly ApplicationDbContext db;

    public InfluencerDashboardController(UserManager<ApplicationUser> userManager, ApplicationDbContext db)
        : base(userManager, db)
    {
        this.db = db;
    }

    public async Task<IActionResult> Index(string? period, bool allActivity = false)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var collaborations = await db.Collaborations.AsNoTracking().AsSplitQuery()
            .Where(c => c.InfluencerProfileId == influencer.Id)
            .Include(c => c.Campaign).ThenInclude(c => c.BrandProfile)
            .Include(c => c.Milestones)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
        var payments = await db.Payments.AsNoTracking()
            .Where(p => p.Collaboration.InfluencerProfileId == influencer.Id)
            .Include(p => p.Collaboration).ThenInclude(c => c.Campaign)
            .ToListAsync();
        var notifications = await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == influencer.UserId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        period = period is "last-month" or "year" ? period : "month";
        var start = period == "year" ? new DateTime(now.Year, 1, 1)
            : period == "last-month" ? monthStart.AddMonths(-1) : monthStart;
        var end = period == "year" ? start.AddYears(1) : start.AddMonths(1);
        var periodPayments = payments
            .Where(p => (p.PaidAt ?? p.CreatedAt) >= start && (p.PaidAt ?? p.CreatedAt) < end
                && (p.PaidAt ?? p.CreatedAt) <= now && p.Status != PaymentStatus.Failed)
            .ToList();
        var series = new List<EarningsPoint>();
        for (var bucketStart = start; bucketStart < end;)
        {
            var bucketEnd = period == "year" ? bucketStart.AddMonths(1) : bucketStart.AddDays(7);
            if (bucketEnd > end)
            {
                bucketEnd = end;
            }

            series.Add(new EarningsPoint
            {
                Label = bucketStart.ToString(period == "year" ? "MMM" : "MMM d", CultureInfo.InvariantCulture),
                Amount = periodPayments
                    .Where(p => (p.PaidAt ?? p.CreatedAt) >= bucketStart && (p.PaidAt ?? p.CreatedAt) < bucketEnd)
                    .Sum(p => p.Amount)
            });
            bucketStart = bucketEnd;
        }

        var activeCampaigns = collaborations
            .Where(c => c.Status == CollaborationStatus.Active)
            .Take(5)
            .Select(c =>
            {
                var total = c.Milestones.Count;
                var completed = c.Milestones.Count(m => m.Status is MilestoneStatus.Approved or MilestoneStatus.Paid);
                return new ActiveCampaignRow
                {
                    CampaignId = c.CampaignId,
                    CollaborationId = c.Id,
                    CampaignTitle = c.Campaign.Title,
                    BrandName = c.Campaign.BrandProfile.CompanyName,
                    Platform = c.Campaign.Platform,
                    TotalValue = c.Milestones.Sum(m => m.Amount),
                    ProgressPercent = total == 0 ? 0 : (int)Math.Round(completed * 100.0 / total),
                    CompletedMilestones = completed,
                    TotalMilestones = total
                };
            })
            .ToList();
        var upcomingMilestones = collaborations
            .Where(c => c.Status == CollaborationStatus.Active)
            .SelectMany(c => c.Milestones.Select(m => new { Collaboration = c, Milestone = m }))
            .Where(x => x.Milestone.Status is MilestoneStatus.Pending or MilestoneStatus.Submitted or MilestoneStatus.RevisionRequested)
            .OrderBy(x => x.Milestone.DueDate ?? DateTime.MaxValue)
            .ThenBy(x => x.Milestone.Id)
            .Take(5)
            .Select(x => new UpcomingMilestoneRow
            {
                CampaignId = x.Collaboration.CampaignId,
                MilestoneId = x.Milestone.Id,
                CampaignTitle = x.Collaboration.Campaign.Title,
                BrandName = x.Collaboration.Campaign.BrandProfile.CompanyName,
                MilestoneTitle = x.Milestone.Title,
                Amount = x.Milestone.Amount,
                DueDate = x.Milestone.DueDate,
                Status = x.Milestone.Status
            })
            .ToList();
        var activity = collaborations.Where(c => c.CreatedAt <= now)
            .Select(c => new DashboardActivity("Joined a campaign", c.Campaign.Title, c.CreatedAt))
            .Concat(payments.Where(p => p.Status == PaymentStatus.Completed && (p.PaidAt ?? p.CreatedAt) <= now)
                .Select(p => new DashboardActivity("Payment received", p.Collaboration.Campaign.Title, p.PaidAt ?? p.CreatedAt)))
            .Concat(notifications.Where(n => n.CreatedAt <= now)
                .Select(n => new DashboardActivity(n.Title, n.Body, n.CreatedAt)))
            .OrderByDescending(a => a.Date)
            .ToList();

        return View(new InfluencerDashboardViewModel
        {
            Profile = influencer,
            ActiveCampaignCount = collaborations.Count(c => c.Status == CollaborationStatus.Active),
            CompletedCampaignCount = collaborations.Count(c => c.Status == CollaborationStatus.Completed),
            PendingEarnings = payments.Where(p => p.Status == PaymentStatus.Pending).Sum(p => p.Amount),
            TotalEarnings = payments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount),
            MonthPaid = periodPayments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount),
            MonthPending = periodPayments.Where(p => p.Status == PaymentStatus.Pending).Sum(p => p.Amount),
            ActiveCampaigns = activeCampaigns,
            UpcomingMilestones = upcomingMilestones,
            EarningsSeries = series,
            Period = period,
            PeriodLabel = period == "year" ? start.ToString("yyyy") : start.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
            AllActivity = allActivity,
            Activity = allActivity ? activity : activity.Take(4).ToList(),
            Notifications = notifications.Take(5).ToList()
        });
    }
}