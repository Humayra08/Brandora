using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Areas.Admin.Controllers;

public record RecentActivityItem(string Title, string Category, string Url, DateTime CreatedAt, string StatusLabel, string StatusColor);

public class AdminDashboardViewModel
{
    public int TotalUsers { get; set; }
    public int PendingVerifications { get; set; }
    public int PendingProofReviews { get; set; }
    public int OpenDisputes { get; set; }
    public decimal TotalTransactionVolume { get; set; }
    public List<RecentActivityItem> RecentActivity { get; set; } = new();
}

public class AdminDashboardController(ApplicationDbContext db) : AdminControllerBase(db)
{
    public async Task<IActionResult> Index()
    {
        await LoadAdminChromeAsync();
        ViewData["ActiveNav"] = "Dashboard";
        ViewData["Title"] = "Admin Dashboard";

        var vm = new AdminDashboardViewModel
        {
            TotalUsers = await db.Users.CountAsync(),
            PendingVerifications = await db.InfluencerProfiles.CountAsync(i => !i.Verified),
            PendingProofReviews = await db.Milestones.CountAsync(m => m.Status == MilestoneStatus.Submitted),
            OpenDisputes = await db.Disputes.CountAsync(d => d.Status == DisputeStatus.Open),
            TotalTransactionVolume = await db.Payments.Where(p => p.Status == PaymentStatus.Completed).SumAsync(p => (decimal?)p.Amount) ?? 0m
        };

        var recentVerifications = await db.InfluencerProfiles
            .OrderByDescending(i => i.CreatedAt)
            .Take(5)
            .Select(i => new RecentActivityItem(
                i.FullName + " registered",
                "Verification",
                "/Admin/AdminUserVerification/Details?type=influencer&id=" + i.Id,
                i.CreatedAt,
                i.Verified ? "Verified" : "Pending",
                i.Verified ? "green" : "gray"))
            .ToListAsync();

        var recentProofs = await db.Milestones
            .Where(m => m.ProofUrl != null)
            .OrderByDescending(m => m.CreatedAt)
            .Take(5)
            .Select(m => new RecentActivityItem(
                "Proof submitted: " + m.Title,
                "Proof Review",
                "/Admin/AdminProofReview/Details/" + m.Id,
                m.CreatedAt,
                m.Status.ToString(),
                m.Status == MilestoneStatus.Approved || m.Status == MilestoneStatus.Paid ? "green" :
                    m.Status == MilestoneStatus.Submitted ? "blue" :
                    m.Status == MilestoneStatus.RevisionRequested ? "red" : "gray"))
            .ToListAsync();

        var recentDisputes = await db.Disputes
            .OrderByDescending(d => d.CreatedAt)
            .Take(5)
            .Select(d => new RecentActivityItem(
                "Dispute: " + d.Reason,
                "Dispute",
                "/Admin/AdminDisputes/Details/" + d.Id,
                d.CreatedAt,
                d.Status.ToString(),
                d.Status == DisputeStatus.Resolved ? "green" : d.Status == DisputeStatus.UnderReview ? "blue" : "gray"))
            .ToListAsync();

        vm.RecentActivity = recentVerifications
            .Concat(recentProofs)
            .Concat(recentDisputes)
            .OrderByDescending(a => a.CreatedAt)
            .Take(10)
            .ToList();

        return View(vm);
    }
}
