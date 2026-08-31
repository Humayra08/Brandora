using System.Security.Claims;
using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Areas.Admin.Controllers;

public record AdminAlert(string Title, string Url, DateTime CreatedAt, string Category);

[Area("Admin")]
[Authorize(AuthenticationSchemes = "AdminScheme")]
public abstract class AdminControllerBase(ApplicationDbContext db) : Controller
{
    protected string AdminName => User.FindFirstValue(ClaimTypes.Name) ?? "Administrator";
    protected string AdminEmail => User.FindFirstValue(ClaimTypes.Email) ?? "";

    protected async Task LoadAdminChromeAsync()
    {
        ViewData["AdminName"] = AdminName;
        ViewData["AdminEmail"] = AdminEmail;
        ViewData["PendingVerifications"] = await db.InfluencerProfiles.CountAsync(i => !i.Verified);
        ViewData["PendingProofReviews"] = await db.Milestones.CountAsync(m => m.Status == MilestoneStatus.Submitted);
        ViewData["OpenDisputes"] = await db.Disputes.CountAsync(d => d.Status == DisputeStatus.Open);
        ViewData["RecentAlerts"] = await BuildRecentAlertsAsync();
    }

    private async Task<List<AdminAlert>> BuildRecentAlertsAsync()
    {
        var verificationAlerts = await db.InfluencerProfiles
            .Where(i => !i.Verified)
            .OrderByDescending(i => i.CreatedAt)
            .Take(5)
            .Select(i => new AdminAlert(i.FullName + " awaiting verification", "/Admin/AdminUserVerification/Details?type=influencer&id=" + i.Id, i.CreatedAt, "Verification"))
            .ToListAsync();

        var proofAlerts = await db.Milestones
            .Where(m => m.Status == MilestoneStatus.Submitted)
            .OrderByDescending(m => m.CreatedAt)
            .Take(5)
            .Select(m => new AdminAlert("Proof submitted: " + m.Title, "/Admin/AdminProofReview/Details/" + m.Id, m.CreatedAt, "Proof"))
            .ToListAsync();

        var disputeAlerts = await db.Disputes
            .Where(d => d.Status == DisputeStatus.Open)
            .OrderByDescending(d => d.CreatedAt)
            .Take(5)
            .Select(d => new AdminAlert("Dispute opened: " + d.Reason, "/Admin/AdminDisputes/Details/" + d.Id, d.CreatedAt, "Dispute"))
            .ToListAsync();

        return verificationAlerts
            .Concat(proofAlerts)
            .Concat(disputeAlerts)
            .OrderByDescending(a => a.CreatedAt)
            .Take(5)
            .ToList();
    }
}
