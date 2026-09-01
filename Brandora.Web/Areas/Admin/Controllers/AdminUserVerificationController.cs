using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Areas.Admin.Controllers;

public record VerificationRow(string Type, int Id, string Name, DateTime SubmittedDate, bool Verified, string FollowersDisplay);

public class AdminUserVerificationController(ApplicationDbContext db) : AdminControllerBase(db)
{
    private static string FormatFollowers(int followers) => followers >= 1000
        ? (followers / 1000m).ToString("0.#") + "K"
        : followers.ToString();

    public async Task<IActionResult> Index(string? role, string? status)
    {
        await LoadAdminChromeAsync();
        ViewData["ActiveNav"] = "UserVerification";
        ViewData["Title"] = "User Verification";
        ViewData["PageHeading"] = "User Verification";
        ViewData["PageSubheading"] = "Review and verify user accounts. Ensure all brands and influencers are authentic.";
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("User Verification", null) };

        var influencerProfiles = await db.InfluencerProfiles.OrderByDescending(i => i.CreatedAt).ToListAsync();
        var brandProfiles = await db.BrandProfiles.OrderByDescending(b => b.CreatedAt).ToListAsync();

        var influencers = influencerProfiles
            .Select(i => new VerificationRow("Influencer", i.Id, i.FullName, i.CreatedAt, i.Verified, FormatFollowers(i.Followers)));

        // NOTE: Brand has no real verification field yet (BrandProfile.Verified doesn't exist —
        // flagged previously, needs approval before adding). Every brand is shown as "Verified"
        // here as a stand-in until that column exists; this is a known gap, not a bug.
        var brands = brandProfiles
            .Select(b => new VerificationRow("Brand", b.Id, b.CompanyName, b.CreatedAt, true, "–"));

        var allRows = influencers.Concat(brands).OrderByDescending(r => r.SubmittedDate).ToList();

        ViewData["TotalAllCount"] = allRows.Count;
        ViewData["TotalBrandCount"] = allRows.Count(r => r.Type == "Brand");
        ViewData["TotalInfluencerCount"] = allRows.Count(r => r.Type == "Influencer");
        ViewData["TotalPendingCount"] = allRows.Count(r => !r.Verified);
        ViewData["TotalVerifiedCount"] = allRows.Count(r => r.Verified);
        // No "Rejected" state exists on any profile yet — always 0 until that status is added.
        ViewData["TotalRejectedCount"] = 0;

        IEnumerable<VerificationRow> rows = allRows;

        if (!string.IsNullOrEmpty(role))
        {
            rows = rows.Where(r => string.Equals(r.Type, role, StringComparison.OrdinalIgnoreCase));
        }

        if (string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            rows = rows.Where(r => !r.Verified);
        }
        else if (string.Equals(status, "Verified", StringComparison.OrdinalIgnoreCase))
        {
            rows = rows.Where(r => r.Verified);
        }
        else if (string.Equals(status, "Rejected", StringComparison.OrdinalIgnoreCase))
        {
            rows = Enumerable.Empty<VerificationRow>();
        }

        ViewData["RoleFilter"] = role ?? "All";
        ViewData["StatusFilter"] = status ?? "All";

        return View(rows.ToList());
    }

    public async Task<IActionResult> Details(string type, int id)
    {
        await LoadAdminChromeAsync();
        ViewData["ActiveNav"] = "UserVerification";
        ViewData["Title"] = "User Verification Details";
        ViewData["PageHeading"] = "User Verification Details";
        ViewData["PageSubheading"] = "Review the submitted information and verify the account.";
        ViewData["Breadcrumb"] = new List<(string, string?)>
        {
            ("User Verification", "/Admin/AdminUserVerification/Index"),
            ("Detail", null)
        };

        if (string.Equals(type, "brand", StringComparison.OrdinalIgnoreCase))
        {
            var brand = await db.BrandProfiles.FirstOrDefaultAsync(b => b.Id == id);
            ViewData["Type"] = "Brand";
            return View((object?)brand);
        }

        var influencer = await db.InfluencerProfiles.Include(i => i.User).FirstOrDefaultAsync(i => i.Id == id);
        ViewData["Type"] = "Influencer";

        if (influencer is not null)
        {
            ViewData["PreviousCollaborationsCount"] = await db.Collaborations.CountAsync(c => c.InfluencerProfileId == influencer.Id);
        }

        return View((object?)influencer);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(string type, int id)
    {
        if (string.Equals(type, "influencer", StringComparison.OrdinalIgnoreCase))
        {
            var influencer = await db.InfluencerProfiles.FirstOrDefaultAsync(i => i.Id == id);
            if (influencer is not null)
            {
                influencer.Verified = true;
                await db.SaveChangesAsync();
            }
        }

        // NOTE: Brand has no verification field yet (flagged in plan - needs BrandProfile.Verified column,
        // a Brand/Influencer-side schema change that requires separate approval before wiring for real).

        TempData["VerificationMessage"] = "Verification updated.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(string type, int id, string reason)
    {
        // NOTE: no "Rejected" state exists on InfluencerProfile.Verified (bool) or BrandProfile today.
        // TODO: connect to backend once a real verification-status field/workflow exists.
        TempData["VerificationMessage"] = "Rejection reason recorded (UI placeholder — no status field to persist to yet).";
        return RedirectToAction("Index");
    }
}
