using Brandora.Web.Data;
using Brandora.Web.Models.Dashboard;
using Brandora.Web.Models.Domain;
using Brandora.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Controllers;

public class UploadProofController(UserManager<ApplicationUser> userManager, ApplicationDbContext db, MediaUploadService mediaUploads, NotificationService notifications) : InfluencerControllerBase(userManager, db)
{
    public async Task<IActionResult> Index()
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var collaborations = await db.Collaborations
            .Where(c => c.InfluencerProfileId == influencer.Id && c.Status == CollaborationStatus.Active)
            .Include(c => c.Campaign).ThenInclude(camp => camp.BrandProfile)
            .Include(c => c.Milestones)
            .ToListAsync();

        var campaigns = collaborations
            .Select(c => new ProofCampaignOption
            {
                CollaborationId = c.Id,
                CampaignId = c.Campaign.Id,
                Title = c.Campaign.Title,
                BrandName = c.Campaign.BrandProfile.CompanyName,
                Platform = c.Campaign.Platform,
                Niche = c.Campaign.Niche,
                MediaUrl = c.Campaign.MediaUrl,
                Budget = c.Campaign.Budget,
                Deadline = c.Campaign.Deadline,
                Milestones = c.Milestones
                    .Where(m => m.Status == MilestoneStatus.Pending || m.Status == MilestoneStatus.RevisionRequested)
                    .OrderBy(m => m.DueDate ?? DateTime.MaxValue)
                    .Select(m => new ProofMilestoneOption { MilestoneId = m.Id, Title = m.Title, Amount = m.Amount })
                    .ToList()
            })
            .Where(c => c.Milestones.Count > 0)
            .OrderBy(c => c.Title)
            .ToList();

        return View(new UploadProofViewModel { Profile = influencer, Campaigns = campaigns });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(int milestoneId, string? proofUrl, string? proofNotes, IFormFile? proofFile)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var milestone = await db.Milestones
            .Include(m => m.Collaboration).ThenInclude(c => c.Campaign).ThenInclude(camp => camp.BrandProfile)
            .FirstOrDefaultAsync(m => m.Id == milestoneId && m.Collaboration.InfluencerProfileId == influencer.Id);

        if (milestone is null)
        {
            return NotFound();
        }

        string? finalProofUrl = null;

        if (proofFile is { Length: > 0 })
        {
            var (url, _, error) = await mediaUploads.SaveMediaAsync(proofFile, "proofs");
            if (error is not null)
            {
                TempData["ProofError"] = error;
                return RedirectToAction("Index");
            }

            finalProofUrl = url;
        }
        else if (!string.IsNullOrWhiteSpace(proofUrl))
        {
            finalProofUrl = proofUrl;
        }

        if (finalProofUrl is null)
        {
            TempData["ProofError"] = "Upload a file or add a post link before submitting.";
            return RedirectToAction("Index");
        }

        milestone.ProofUrl = finalProofUrl;
        milestone.ProofNotes = proofNotes;
        milestone.Status = MilestoneStatus.Submitted;

        notifications.Notify(
            milestone.Collaboration.Campaign.BrandProfile.UserId,
            "Milestone",
            "Proof submitted",
            $"{influencer.FullName} submitted proof for \"{milestone.Title}\" on {milestone.Collaboration.Campaign.Title}.",
            $"/Milestones/Detail/{milestone.Id}");

        await db.SaveChangesAsync();

        TempData["ProofSubmitted"] = "true";
        return RedirectToAction("Index");
    }
}
