using Brandora.Web.Data;
using Brandora.Web.Models.Dashboard;
using Brandora.Web.Models.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Controllers;

public class InfluencerPaymentsController(UserManager<ApplicationUser> userManager, ApplicationDbContext db)
    : InfluencerControllerBase(userManager, db)
{
    public async Task<IActionResult> Index(string? status, string? range, int page = 1, bool preview = false)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var payments = await db.Payments.AsNoTracking()
            .Where(p => p.Collaboration.InfluencerProfileId == influencer.Id)
            .Include(p => p.Collaboration).ThenInclude(c => c.Campaign).ThenInclude(c => c.BrandProfile)
            .Include(p => p.Milestone)
            .OrderByDescending(p => p.PaidAt ?? p.CreatedAt).ThenByDescending(p => p.Id)
            .ToListAsync();

        var vm = new InfluencerPaymentsViewModel
        {
            Profile = influencer,
            Notifications = await db.Notifications.AsNoTracking()
                .Where(n => n.UserId == influencer.UserId)
                .OrderByDescending(n => n.CreatedAt).Take(5).ToListAsync(),
            IsPreview = preview,
            Status = status is "paid" or "processing" or "failed" ? status : "all",
            Range = range is "30days" or "3months" or "year" ? range : "all"
        };

        var rows = preview
            ? ExamplePayments()
            : payments.Select(p => new PaymentHistoryRow
            {
                Date = p.PaidAt ?? p.CreatedAt,
                CampaignId = p.Collaboration.CampaignId,
                CampaignTitle = p.Collaboration.Campaign.Title,
                BrandName = p.Collaboration.Campaign.BrandProfile.CompanyName,
                MilestoneTitle = p.Milestone?.Title ?? "Campaign payment",
                Amount = p.Amount,
                Status = p.Status
            }).ToList();

        vm.TotalEarnings = rows.Where(r => r.Status != PaymentStatus.Failed).Sum(r => r.Amount);
        vm.CompletedAmount = rows.Where(r => r.Status == PaymentStatus.Completed).Sum(r => r.Amount);
        vm.PendingAmount = rows.Where(r => r.Status == PaymentStatus.Pending).Sum(r => r.Amount);
        vm.CompletedCount = rows.Count(r => r.Status == PaymentStatus.Completed);
        vm.PendingCount = rows.Count(r => r.Status == PaymentStatus.Pending);
        vm.GrowthPercent = preview ? 12 : MonthOverMonthGrowth(rows);
        vm.PayoutMethods = preview ? ExamplePayoutMethods() : new List<PayoutMethodRow>();

        var cutoff = vm.Range switch
        {
            "30days" => DateTime.UtcNow.AddDays(-30),
            "3months" => DateTime.UtcNow.AddMonths(-3),
            "year" => DateTime.UtcNow.AddYears(-1),
            _ => (DateTime?)null
        };

        var filtered = rows
            .Where(r => vm.Status switch
            {
                "paid" => r.Status == PaymentStatus.Completed,
                "processing" => r.Status == PaymentStatus.Pending,
                "failed" => r.Status == PaymentStatus.Failed,
                _ => true
            })
            .Where(r => !cutoff.HasValue || r.Date >= cutoff.Value)
            .ToList();

        vm.TotalCount = filtered.Count;
        vm.Page = Math.Clamp(page, 1, vm.TotalPages);
        vm.Payments = filtered.Skip((vm.Page - 1) * vm.PageSize).Take(vm.PageSize).ToList();

        return View(vm);
    }

    private static int? MonthOverMonthGrowth(List<PaymentHistoryRow> rows)
    {
        var now = DateTime.UtcNow;
        var thisMonth = new DateTime(now.Year, now.Month, 1);
        var lastMonth = thisMonth.AddMonths(-1);

        decimal EarnedBetween(DateTime from, DateTime to) => rows
            .Where(r => r.Status == PaymentStatus.Completed && r.Date >= from && r.Date < to)
            .Sum(r => r.Amount);

        var previous = EarnedBetween(lastMonth, thisMonth);
        if (previous <= 0)
        {
            return null;
        }

        return (int)Math.Round((EarnedBetween(thisMonth, now.AddDays(1)) - previous) / previous * 100);
    }

    // Values from the reference design, shown only under ?preview=true.
    private static List<PaymentHistoryRow> ExamplePayments() =>
    [
        new() { Date = new DateTime(2026, 9, 10), CampaignTitle = "Summer Style Stories", BrandName = "Zara", MilestoneTitle = "Instagram Reel", Amount = 4000, Status = PaymentStatus.Completed, Method = "Bank Transfer", TransactionId = "BRD567830" },
        new() { Date = new DateTime(2026, 8, 28), CampaignTitle = "Glow Naturally", BrandName = "The Body Shop", MilestoneTitle = "TikTok Video", Amount = 5500, Status = PaymentStatus.Completed, Method = "bKash", TransactionId = "BRD498221" },
        new() { Date = new DateTime(2026, 8, 12), CampaignTitle = "Tech for Tomorrow", BrandName = "Samsung", MilestoneTitle = "Instagram Reel", Amount = 6000, Status = PaymentStatus.Pending, Method = "Bank Transfer", TransactionId = "BRD445612" },
        new() { Date = new DateTime(2026, 8, 2), CampaignTitle = "Everyday Essentials", BrandName = "Daraz", MilestoneTitle = "Facebook Live", Amount = 4500, Status = PaymentStatus.Completed, Method = "Nagad", TransactionId = "BRD332145" },
        new() { Date = new DateTime(2026, 7, 18), CampaignTitle = "Style Your Way", BrandName = "H&M", MilestoneTitle = "TikTok Video", Amount = 7000, Status = PaymentStatus.Completed, Method = "Bank Transfer", TransactionId = "BRD221098" },
    ];

    private static List<PayoutMethodRow> ExamplePayoutMethods() =>
    [
        new() { Name = "Bank Account", Detail = "BRAC Bank ********1234", Kind = "bank", IsPrimary = true },
        new() { Name = "bKash", Detail = "017********", Kind = "bkash" },
    ];
}
