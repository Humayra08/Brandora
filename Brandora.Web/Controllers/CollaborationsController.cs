using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Brandora.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Controllers;

public class CollaborationsController(UserManager<ApplicationUser> userManager, ApplicationDbContext db, NotificationService notifications) : BrandControllerBase(userManager, db)
{
    public async Task<IActionResult> Index(int? campaignId)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var query = db.Collaborations.Where(c => c.Campaign.BrandProfileId == brand.Id);

        if (campaignId.HasValue)
        {
            query = query.Where(c => c.CampaignId == campaignId.Value);
        }

        var collaborations = await query
            .Include(c => c.Campaign)
            .Include(c => c.InfluencerProfile)
            .Include(c => c.Milestones).ThenInclude(m => m.Payment)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return View(collaborations);
    }

    public async Task<IActionResult> Detail(int id)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var collaboration = await db.Collaborations
            .Include(c => c.Campaign)
            .Include(c => c.InfluencerProfile)
            .Include(c => c.Proposal)
            .Include(c => c.Milestones).ThenInclude(m => m.Payment)
            .FirstOrDefaultAsync(c => c.Id == id && c.Campaign.BrandProfileId == brand.Id);

        if (collaboration is null)
        {
            return NotFound();
        }

        var conversation = await db.Conversations.FirstOrDefaultAsync(c =>
            c.BrandProfileId == brand.Id
            && c.InfluencerProfileId == collaboration.InfluencerProfileId
            && c.CampaignId == collaboration.CampaignId);

        ViewData["ConversationId"] = conversation?.Id;

        return View(collaboration);
    }

    // Brand can only ever move a collaboration forward from Active to Completed or
    // Cancelled — never back to Active, and never to an arbitrary enum value straight
    // from the request. Completing also requires every milestone to already be Paid,
    // so a Brand can't close out a collaboration to make outstanding work disappear
    // from their active list while still owing the influencer payment.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, CollaborationStatus status)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var collaboration = await db.Collaborations
            .Include(c => c.Campaign)
            .Include(c => c.InfluencerProfile)
            .Include(c => c.Milestones)
            .FirstOrDefaultAsync(c => c.Id == id && c.Campaign.BrandProfileId == brand.Id);

        if (collaboration is null)
        {
            return NotFound();
        }

        if (collaboration.Status != CollaborationStatus.Active ||
            (status != CollaborationStatus.Completed && status != CollaborationStatus.Cancelled))
        {
            return RedirectToAction("Detail", new { id });
        }

        if (status == CollaborationStatus.Completed &&
            collaboration.Milestones.Any(m => m.Status != MilestoneStatus.Paid))
        {
            TempData["CollaborationError"] = "Every milestone needs to be paid before this collaboration can be marked complete.";
            return RedirectToAction("Detail", new { id });
        }

        collaboration.Status = status;
        await db.SaveChangesAsync();

        await notifications.NotifyAsync(
            collaboration.InfluencerProfile.UserId,
            "Collaboration",
            status == CollaborationStatus.Completed ? "Collaboration completed" : "Collaboration cancelled",
            status == CollaborationStatus.Completed
                ? $"{brand.CompanyName} marked your collaboration on \"{collaboration.Campaign.Title}\" as completed."
                : $"{brand.CompanyName} cancelled your collaboration on \"{collaboration.Campaign.Title}\".",
            $"/InfluencerCampaigns");

        return RedirectToAction("Detail", new { id });
    }
}
