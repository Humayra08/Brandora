using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Brandora.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Areas.Admin.Controllers;

public class AdminProofReviewController(ApplicationDbContext db, NotificationService notifications) : AdminControllerBase(db)
{
    public async Task<IActionResult> Index(MilestoneStatus? status)
    {
        await LoadAdminChromeAsync();
        ViewData["ActiveNav"] = "ProofReview";
        ViewData["Title"] = "Proof-of-Post Review";
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Proof-of-Post Review", null) };
        ViewData["StatusFilter"] = status;

        var query = db.Milestones
            .Include(m => m.Collaboration).ThenInclude(c => c.Campaign)
            .Include(m => m.Collaboration).ThenInclude(c => c.InfluencerProfile)
            .Where(m => m.ProofUrl != null)
            .AsQueryable();

        if (status is not null)
        {
            query = query.Where(m => m.Status == status);
        }

        var milestones = await query.OrderByDescending(m => m.CreatedAt).ToListAsync();
        return View(milestones);
    }

    public async Task<IActionResult> Details(int id)
    {
        await LoadAdminChromeAsync();
        ViewData["ActiveNav"] = "ProofReview";
        ViewData["Title"] = "Proof Detail";
        ViewData["Breadcrumb"] = new List<(string, string?)>
        {
            ("Proof-of-Post Review", "/Admin/AdminProofReview/Index"),
            ("Detail", null)
        };

        var milestone = await db.Milestones
            .Include(m => m.Collaboration).ThenInclude(c => c.Campaign).ThenInclude(c => c.BrandProfile)
            .Include(m => m.Collaboration).ThenInclude(c => c.InfluencerProfile)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (milestone is null) return NotFound();

        return View(milestone);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        var milestone = await db.Milestones
            .Include(m => m.Collaboration).ThenInclude(c => c.InfluencerProfile)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (milestone is null) return NotFound();

        if (milestone.Status == MilestoneStatus.Submitted)
        {
            milestone.Status = MilestoneStatus.Approved;

            notifications.Notify(
                milestone.Collaboration.InfluencerProfile.UserId,
                "Milestone",
                "Milestone approved",
                $"\"{milestone.Title}\" was approved by Admin and is ready for payment.",
                $"/Milestones/Detail/{milestone.Id}");

            await db.SaveChangesAsync();
        }

        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string comment)
    {
        var milestone = await db.Milestones
            .Include(m => m.Collaboration).ThenInclude(c => c.InfluencerProfile)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (milestone is null) return NotFound();

        if (milestone.Status == MilestoneStatus.Submitted)
        {
            milestone.Status = MilestoneStatus.RevisionRequested;
            milestone.ProofNotes = comment;

            notifications.Notify(
                milestone.Collaboration.InfluencerProfile.UserId,
                "Milestone",
                "Revision requested",
                $"Admin requested a revision for \"{milestone.Title}\": {comment}",
                $"/Milestones/Detail/{milestone.Id}");

            await db.SaveChangesAsync();
        }

        return RedirectToAction("Index");
    }
}
