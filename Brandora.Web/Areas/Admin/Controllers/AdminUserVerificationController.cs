using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Brandora.Web.Services.Email;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Areas.Admin.Controllers;

public record VerificationRow(string Type, int Id, string Name, DateTime SubmittedDate, VerificationStatus Status, string FollowersDisplay);

public class AdminUserVerificationController(ApplicationDbContext db, IEmailSender emailSender) : AdminControllerBase(db)
{
    private static string FormatFollowers(int followers) => followers >= 1000
        ? (followers / 1000m).ToString("0.#") + "K"
        : followers.ToString();

    public async Task<IActionResult> Index(string? role, string? status)
    {
        await LoadAdminChromeAsync();
        ViewData["ActiveNav"] = "UserVerification";
        ViewData["Title"] = "User Verification";
        // This page has its own hero card with the title, so the shared topbar's plain
        // heading/subheading text is suppressed. The breadcrumb renders in the topbar,
        // outside the hero card itself (see _AdminLayout).
        ViewData["HasCustomHero"] = true;
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("User Verification", null) };

        var influencerProfiles = await db.InfluencerProfiles.OrderByDescending(i => i.CreatedAt).ToListAsync();
        var brandProfiles = await db.BrandProfiles.OrderByDescending(b => b.CreatedAt).ToListAsync();

        var influencers = influencerProfiles
            .Select(i => new VerificationRow("Influencer", i.Id, i.FullName, i.CreatedAt, i.VerificationStatus, FormatFollowers(i.Followers)));

        var brands = brandProfiles
            .Select(b => new VerificationRow("Brand", b.Id, b.CompanyName, b.CreatedAt, b.VerificationStatus, "–"));

        var allRows = influencers.Concat(brands).OrderByDescending(r => r.SubmittedDate).ToList();

        ViewData["TotalAllCount"] = allRows.Count;
        ViewData["TotalBrandCount"] = allRows.Count(r => r.Type == "Brand");
        ViewData["TotalInfluencerCount"] = allRows.Count(r => r.Type == "Influencer");
        ViewData["TotalPendingCount"] = allRows.Count(r => r.Status == VerificationStatus.Pending);
        ViewData["TotalVerifiedCount"] = allRows.Count(r => r.Status == VerificationStatus.Verified);
        ViewData["TotalRejectedCount"] = allRows.Count(r => r.Status == VerificationStatus.Rejected);
        ViewData["NewTodayCount"] = allRows.Count(r => r.SubmittedDate.Date == DateTime.UtcNow.Date);

        IEnumerable<VerificationRow> rows = allRows;

        if (!string.IsNullOrEmpty(role))
        {
            rows = rows.Where(r => string.Equals(r.Type, role, StringComparison.OrdinalIgnoreCase));
        }

        if (string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            rows = rows.Where(r => r.Status == VerificationStatus.Pending);
        }
        else if (string.Equals(status, "Verified", StringComparison.OrdinalIgnoreCase))
        {
            rows = rows.Where(r => r.Status == VerificationStatus.Verified);
        }
        else if (string.Equals(status, "Rejected", StringComparison.OrdinalIgnoreCase))
        {
            rows = rows.Where(r => r.Status == VerificationStatus.Rejected);
        }

        ViewData["RoleFilter"] = role ?? "All";
        ViewData["StatusFilter"] = status ?? "All";

        return View(rows.ToList());
    }

    public async Task<IActionResult> Details(string type, int id)
    {
        await LoadAdminChromeAsync();
        ViewData["ActiveNav"] = "UserVerification";
        ViewData["Title"] = "Review Profile";
        ViewData["HasCustomHero"] = true;
        ViewData["Breadcrumb"] = new List<(string, string?)>
        {
            ("User Verification", "/Admin/AdminUserVerification/Index"),
            ("Review", null)
        };

        if (string.Equals(type, "brand", StringComparison.OrdinalIgnoreCase))
        {
            var brand = await db.BrandProfiles.Include(b => b.User).FirstOrDefaultAsync(b => b.Id == id);
            ViewData["Type"] = "Brand";
            if (brand is not null)
            {
                ViewData["CampaignCount"] = await db.Campaigns.CountAsync(c => c.BrandProfileId == brand.Id);
            }
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
            var influencer = await db.InfluencerProfiles.Include(i => i.User).FirstOrDefaultAsync(i => i.Id == id);
            if (influencer is not null)
            {
                influencer.Verified = true;
                influencer.VerificationStatus = VerificationStatus.Verified;
                influencer.VerifiedAt = DateTime.UtcNow;
                influencer.RejectionReason = null;
                await db.SaveChangesAsync();
                await SendApprovedEmailAsync(influencer.User.Email!, influencer.FullName);
            }
        }
        else if (string.Equals(type, "brand", StringComparison.OrdinalIgnoreCase))
        {
            var brand = await db.BrandProfiles.Include(b => b.User).FirstOrDefaultAsync(b => b.Id == id);
            if (brand is not null)
            {
                brand.VerificationStatus = VerificationStatus.Verified;
                brand.VerifiedAt = DateTime.UtcNow;
                brand.RejectionReason = null;
                await db.SaveChangesAsync();
                await SendApprovedEmailAsync(brand.User.Email!, brand.ContactFullName);
            }
        }

        TempData["VerificationMessage"] = "User approved.";
        return RedirectToAction("Details", new { type, id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(string type, int id, string reason)
    {
        if (string.Equals(type, "influencer", StringComparison.OrdinalIgnoreCase))
        {
            var influencer = await db.InfluencerProfiles.Include(i => i.User).FirstOrDefaultAsync(i => i.Id == id);
            if (influencer is not null)
            {
                influencer.Verified = false;
                influencer.VerificationStatus = VerificationStatus.Rejected;
                influencer.RejectionReason = reason;
                influencer.VerifiedAt = null;
                await db.SaveChangesAsync();
                await SendRejectedEmailAsync(influencer.User.Email!, influencer.FullName, reason);
            }
        }
        else if (string.Equals(type, "brand", StringComparison.OrdinalIgnoreCase))
        {
            var brand = await db.BrandProfiles.Include(b => b.User).FirstOrDefaultAsync(b => b.Id == id);
            if (brand is not null)
            {
                brand.VerificationStatus = VerificationStatus.Rejected;
                brand.RejectionReason = reason;
                brand.VerifiedAt = null;
                await db.SaveChangesAsync();
                await SendRejectedEmailAsync(brand.User.Email!, brand.ContactFullName, reason);
            }
        }

        TempData["VerificationMessage"] = "User rejected.";
        return RedirectToAction("Details", new { type, id });
    }

    private async Task SendApprovedEmailAsync(string email, string fullName)
    {
        var firstName = fullName.Split(' ')[0];
        var loginUrl = Url.Action("Login", "Account", null, Request.Scheme)!;
        var (subject, html) = EmailTemplates.Approved(firstName, loginUrl);
        await emailSender.SendAsync(email, fullName, subject, html);
    }

    private async Task SendRejectedEmailAsync(string email, string fullName, string reason)
    {
        var firstName = fullName.Split(' ')[0];
        var loginUrl = Url.Action("Login", "Account", null, Request.Scheme)!;
        var (subject, html) = EmailTemplates.Rejected(firstName, reason, loginUrl);
        await emailSender.SendAsync(email, fullName, subject, html);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveNotes(string type, int id, string notes)
    {
        if (string.Equals(type, "influencer", StringComparison.OrdinalIgnoreCase))
        {
            var influencer = await db.InfluencerProfiles.FirstOrDefaultAsync(i => i.Id == id);
            if (influencer is not null)
            {
                influencer.AdminNotes = notes;
                await db.SaveChangesAsync();
            }
        }
        else if (string.Equals(type, "brand", StringComparison.OrdinalIgnoreCase))
        {
            var brand = await db.BrandProfiles.FirstOrDefaultAsync(b => b.Id == id);
            if (brand is not null)
            {
                brand.AdminNotes = notes;
                await db.SaveChangesAsync();
            }
        }

        TempData["VerificationMessage"] = "Notes saved.";
        return RedirectToAction("Details", new { type, id });
    }
}
