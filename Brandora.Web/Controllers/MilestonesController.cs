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
            Description = model.Description,
            Amount = model.Amount,
            DueDate = model.DueDate,
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

    public async Task<IActionResult> LogSubmission(int id)
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

        ViewData["Milestone"] = milestone;
        return View(new ProofSubmissionViewModel
        {
            MilestoneId = milestone.Id,
            ProofUrl = milestone.ProofUrl ?? string.Empty,
            ProofNotes = milestone.ProofNotes
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LogSubmission(ProofSubmissionViewModel model)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var milestone = await db.Milestones
            .Include(m => m.Collaboration).ThenInclude(c => c.Campaign)
            .Include(m => m.Collaboration).ThenInclude(c => c.InfluencerProfile)
            .FirstOrDefaultAsync(m => m.Id == model.MilestoneId && m.Collaboration.Campaign.BrandProfileId == brand.Id);

        if (milestone is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            ViewData["Milestone"] = milestone;
            return View(model);
        }

        milestone.ProofUrl = model.ProofUrl;
        milestone.ProofNotes = model.ProofNotes;
        milestone.Status = MilestoneStatus.Submitted;

        notifications.Notify(
            userManager.GetUserId(User)!,
            "Milestone",
            "Submission logged",
            $"{milestone.Collaboration.InfluencerProfile.FullName}'s submission for \"{milestone.Title}\" is ready for review.",
            $"/Milestones/Detail/{milestone.Id}");

        await db.SaveChangesAsync();

        return RedirectToAction("Detail", new { id = milestone.Id });
    }

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

        if (milestone.Status == MilestoneStatus.Submitted)
        {
            milestone.Status = MilestoneStatus.Approved;

            notifications.Notify(
                userManager.GetUserId(User)!,
                "Milestone",
                "Milestone approved",
                $"\"{milestone.Title}\" for {milestone.Collaboration.InfluencerProfile.FullName} is approved and ready for payment.",
                $"/Milestones/Detail/{milestone.Id}");

            await db.SaveChangesAsync();
        }

        return RedirectToAction("Detail", new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestRevision(int id)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var milestone = await db.Milestones
            .Include(m => m.Collaboration).ThenInclude(c => c.Campaign)
            .FirstOrDefaultAsync(m => m.Id == id && m.Collaboration.Campaign.BrandProfileId == brand.Id);

        if (milestone is null)
        {
            return NotFound();
        }

        if (milestone.Status == MilestoneStatus.Submitted)
        {
            milestone.Status = MilestoneStatus.RevisionRequested;
            await db.SaveChangesAsync();
        }

        return RedirectToAction("Detail", new { id });
    }
}
