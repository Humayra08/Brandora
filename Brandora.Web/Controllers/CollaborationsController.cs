using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Controllers;

public class CollaborationsController(UserManager<ApplicationUser> userManager, ApplicationDbContext db) : BrandControllerBase(userManager, db)
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
            .FirstOrDefaultAsync(c => c.Id == id && c.Campaign.BrandProfileId == brand.Id);

        if (collaboration is null)
        {
            return NotFound();
        }

        collaboration.Status = status;
        await db.SaveChangesAsync();

        return RedirectToAction("Detail", new { id });
    }
}
