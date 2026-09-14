using Brandora.Web.Data;
using Brandora.Web.Models.Dashboard;
using Brandora.Web.Models.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Brandora.Web.Controllers;

public class InfluencerEarningsController : InfluencerControllerBase
{
    private readonly ApplicationDbContext db;
    public InfluencerEarningsController(UserManager<ApplicationUser> userManager, ApplicationDbContext db)
        : base(userManager, db) => this.db = db;

    public async Task<IActionResult> Index(string? tab, int? campaignId, string? search, bool allActivity = false)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null) return RedirectToAction("Index", "Home");
        var collaborations = await db.Collaborations.AsNoTracking().AsSplitQuery()
            .Where(c => c.InfluencerProfileId == influencer.Id)
            .Include(c => c.Campaign).ThenInclude(c => c.BrandProfile)
            .Include(c => c.Milestones)
            .OrderByDescending(c => c.CreatedAt).ThenBy(c => c.Id).ToListAsync();
        var campaigns = collaborations.Select(c => c.Campaign).DistinctBy(c => c.Id).OrderBy(c => c.Title).ToList();
        if (campaignId.HasValue && !campaigns.Any(c => c.Id == campaignId.Value)) return NotFound();
        var payments = await db.Payments.AsNoTracking()
            .Where(p => p.Collaboration.InfluencerProfileId == influencer.Id)
            .Include(p => p.Collaboration).ThenInclude(c => c.Campaign).ThenInclude(c => c.BrandProfile)
            .Include(p => p.Milestone)
            .OrderByDescending(p => p.PaidAt ?? p.CreatedAt).ThenByDescending(p => p.Id).ToListAsync();
        var notifications = await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == influencer.UserId)
            .OrderByDescending(n => n.CreatedAt).ToListAsync();
        var completed = payments.Where(p => p.Status == PaymentStatus.Completed).ToList();
        var pending = payments.Where(p => p.Status == PaymentStatus.Pending).ToList();
        var activity = payments.Where(p => p.Status != PaymentStatus.Failed)
            .Select(p => new EarningsActivity(p.Status == PaymentStatus.Completed ? "Payment Received" : "Payment Processing",
                $"৳{p.Amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)} from {p.Collaboration.Campaign.BrandProfile.CompanyName}",
                p.PaidAt ?? p.CreatedAt, p.Status == PaymentStatus.Completed ? "received" : "pending"))
            .Concat(notifications.Where(n => n.Category is "Milestone" or "Proof" or "Collaboration")
                .Select(n => new EarningsActivity(n.Title, n.Body, n.CreatedAt, "proof")))
            .OrderByDescending(a => a.Date).ToList();
        search = search?.Trim();
        bool Matches(Campaign campaign) =>
            (!campaignId.HasValue || campaign.Id == campaignId.Value) &&
            (string.IsNullOrEmpty(search) || campaign.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                campaign.BrandProfile.CompanyName.Contains(search, StringComparison.OrdinalIgnoreCase));
        return View(new InfluencerEarningsViewModel
        {
            InfluencerName = influencer.FullName,
            Tab = tab is "transactions" or "payout-methods" ? tab : "milestones",
            CampaignId = campaignId, Search = search, AllActivity = allActivity,
            Received = completed.Sum(p => p.Amount), Pending = pending.Sum(p => p.Amount),
            ReceivedMilestones = completed.Where(p => p.MilestoneId.HasValue).Select(p => p.MilestoneId).Distinct().Count(),
            PendingMilestones = pending.Where(p => p.MilestoneId.HasValue).Select(p => p.MilestoneId).Distinct().Count(),
            ActiveCampaigns = collaborations.Where(c => c.Status == CollaborationStatus.Active).Select(c => c.CampaignId).Distinct().Count(),
            Campaigns = campaigns,
            Collaborations = collaborations.Where(c => Matches(c.Campaign)).ToList(),
            Payments = payments.Where(p => Matches(p.Collaboration.Campaign)).ToList(),
            Notifications = notifications.Take(5).ToList(),
            Activity = allActivity ? activity : activity.Take(4).ToList()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Withdraw(bool preview = false)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null) return RedirectToAction("Index", "Home");
        var notifications = await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == influencer.UserId)
            .OrderByDescending(n => n.CreatedAt).Take(5).ToListAsync();
        return View(new WithdrawalViewModel
        {
            IsPreview = preview,
            Header = new InfluencerEarningsViewModel
            {
                InfluencerName = influencer.FullName,
                Notifications = notifications
            }
        });
    }
}