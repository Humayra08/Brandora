using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Brandora.Web.Models.Milestones;
using Brandora.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Controllers;

public class MilestonesController(UserManager<ApplicationUser> userManager, ApplicationDbContext db, NotificationService notifications) : BrandControllerBase(userManager, db)
{
    public async Task<IActionResult> Create(int collaborationId)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var collaboration = await db.Collaborations
            .Include(c => c.Campaign)
            .Include(c => c.InfluencerProfile)
            .FirstOrDefaultAsync(c => c.Id == collaborationId && c.Campaign.BrandProfileId == brand.Id);

        if (collaboration is null)
        {
            return NotFound();
        }

        ViewData["Collaboration"] = collaboration;
        return View(new MilestoneFormViewModel { CollaborationId = collaborationId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(MilestoneFormViewModel model)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var collaboration = await db.Collaborations
            .Include(c => c.Campaign)
            .Include(c => c.InfluencerProfile)
            .FirstOrDefaultAsync(c => c.Id == model.CollaborationId && c.Campaign.BrandProfileId == brand.Id);

        if (collaboration is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            ViewData["Collaboration"] = collaboration;
            return View(model);
        }

        var milestone = new Milestone
        {
            CollaborationId = collaboration.Id,
            Title = model.Title,
            ContentType = model.ContentType,
            Description = model.Description,
            Amount = model.Amount,
            DueDate = model.DueDate.HasValue ? DateTime.SpecifyKind(model.DueDate.Value, DateTimeKind.Utc) : null,
            Status = MilestoneStatus.Pending
        };

        db.Milestones.Add(milestone);
        await db.SaveChangesAsync();

        return RedirectToAction("Detail", "Collaborations", new { id = collaboration.Id });
    }

    public async Task<IActionResult> Detail(int id)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var milestone = await db.Milestones
            .Include(m => m.Collaboration).ThenInclude(c => c.Campaign)
            .Include(m => m.Collaboration).ThenInclude(c => c.InfluencerProfile)
            .Include(m => m.Payment)
            .FirstOrDefaultAsync(m => m.Id == id && m.Collaboration.Campaign.BrandProfileId == brand.Id);

        if (milestone is null)
        {
            return NotFound();
        }

        return View(milestone);
    }

    // Brand reviews and signs off on what the influencer already submitted via their own
    // Upload Proof flow (UploadProofController). Brand never authors or edits the proof
    // itself — only Approve / RequestRevision, both of which are independent of Admin's
    // own sign-off (AdminProofReviewController) so a milestone only becomes payment-eligible
    // once BOTH sides have approved it.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var milestone = await db.Milestones
            .Include(m => m.Collaboration).ThenInclude(c => c.Campaign)
            .Include(m => m.Collaboration).ThenInclude(c => c.InfluencerProfile)
            .FirstOrDefaultAsync(m => m.Id == id && m.Collaboration.Campaign.BrandProfileId == brand.Id);

        if (milestone is null)
        {
            return NotFound();
        }

        var canApprove = milestone.BrandApprovedAt is null &&
            (milestone.Status == MilestoneStatus.Submitted || milestone.Status == MilestoneStatus.Approved);

        if (canApprove)
        {
            milestone.BrandApprovedAt = DateTime.UtcNow;

            // Status == Approved here means Admin already signed off before the Brand did —
            // so this Brand approval is the second (final) one, and the milestone is now
            // genuinely payment-eligible. Otherwise this is only the first of the two approvals.
            var bothApproved = milestone.Status == MilestoneStatus.Approved;

            await notifications.NotifyAsync(
                milestone.Collaboration.InfluencerProfile.UserId,
                "Milestone",
                bothApproved ? "Milestone approved — ready for payment" : "Brand approved your submission",
                bothApproved
                    ? $"\"{milestone.Title}\" for {milestone.Collaboration.Campaign.Title} is approved by both Brand and Admin and ready for payment."
                    : $"{brand.CompanyName} approved \"{milestone.Title}\". It's now awaiting Admin's final review before payment.",
                "/InfluencerEarnings");

            await db.SaveChangesAsync();
        }

        return RedirectToAction("Detail", new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestRevision(int id, string reason)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var milestone = await db.Milestones
            .Include(m => m.Collaboration).ThenInclude(c => c.Campaign)
            .Include(m => m.Collaboration).ThenInclude(c => c.InfluencerProfile)
            .FirstOrDefaultAsync(m => m.Id == id && m.Collaboration.Campaign.BrandProfileId == brand.Id);

        if (milestone is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["MilestoneError"] = "Add a reason so the influencer knows what to change.";
            return RedirectToAction("Detail", new { id });
        }

        if (milestone.Status == MilestoneStatus.Submitted || milestone.Status == MilestoneStatus.Approved)
        {
            milestone.Status = MilestoneStatus.RevisionRequested;
            milestone.BrandApprovedAt = null;
            milestone.BrandRevisionReason = reason.Trim();

            await notifications.NotifyAsync(
                milestone.Collaboration.InfluencerProfile.UserId,
                "Milestone",
                "Revision requested",
                $"{brand.CompanyName} requested a revision for \"{milestone.Title}\": {reason.Trim()}",
                "/UploadProof");

            await db.SaveChangesAsync();
        }

        return RedirectToAction("Detail", new { id });
    }
}
