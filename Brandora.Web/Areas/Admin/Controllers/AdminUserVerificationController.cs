using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Areas.Admin.Controllers;

public record VerificationRow(string Type, int Id, string Name, DateTime SubmittedDate, bool Verified);

public class AdminUserVerificationController(ApplicationDbContext db) : AdminControllerBase(db)
{
    public async Task<IActionResult> Index(string? role, string? status)
    {
        await LoadAdminChromeAsync();
        ViewData["ActiveNav"] = "UserVerification";
        ViewData["Title"] = "User Verification";
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("User Verification", null) };

        var influencers = await db.InfluencerProfiles
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new VerificationRow("Influencer", i.Id, i.FullName, i.CreatedAt, i.Verified))
            .ToListAsync();

        var brands = await db.BrandProfiles
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new VerificationRow("Brand", b.Id, b.CompanyName, b.CreatedAt, true))
            .ToListAsync();

        var rows = influencers.Concat(brands).OrderByDescending(r => r.SubmittedDate).AsEnumerable();

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

        ViewData["RoleFilter"] = role ?? "All";
        ViewData["StatusFilter"] = status ?? "All";

        return View(rows.ToList());
    }

    public async Task<IActionResult> Details(string type, int id)
    {
        await LoadAdminChromeAsync();
        ViewData["ActiveNav"] = "UserVerification";
        ViewData["Title"] = "Verification Detail";
        ViewData["Breadcrumb"] = new List<(string, string?)>
        {
            ("User Verification", "/Admin/AdminUserVerification/Index"),
            ("Detail", null)
        };

        if (string.Equals(type, "influencer", StringComparison.OrdinalIgnoreCase))
        {
            var influencer = await db.InfluencerProfiles.FirstOrDefaultAsync(i => i.Id == id);
            if (influencer is null) return NotFound();
            ViewData["Type"] = "Influencer";
            return View((object)influencer);
        }

        if (string.Equals(type, "brand", StringComparison.OrdinalIgnoreCase))
        {
            var brand = await db.BrandProfiles.FirstOrDefaultAsync(b => b.Id == id);
            if (brand is null) return NotFound();
            ViewData["Type"] = "Brand";
            return View((object)brand);
        }

        return NotFound();
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
