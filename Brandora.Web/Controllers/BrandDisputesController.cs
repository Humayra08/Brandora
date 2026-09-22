using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Brandora.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Controllers;

public record BrandDisputeCollabOption(int CollaborationId, string CampaignTitle, string InfluencerName, List<(int Id, string Title)> Milestones);
public record BrandDisputeRow(int Id, string Code, string CampaignTitle, string MilestoneTitle, string Reason, string Status, DateTime CreatedAt, string? ResolutionNotes);

public class BrandDisputesViewModel
{
    public List<BrandDisputeCollabOption> Collaborations { get; set; } = new();
    public List<BrandDisputeRow> MyDisputes { get; set; } = new();
}

// Brand-side "Report a Dispute" — mirrors InfluencerDisputesController exactly, so the same
// milestone reported from either side lands on ONE Dispute row (see the lookup below), and
// Admin's Dispute Resolution page shows both statements on that single case.
public class BrandDisputesController(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext db,
    MediaUploadService mediaUploads,
    NotificationService notifications) : BrandControllerBase(userManager, db)
{
    public async Task<IActionResult> Index()
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null) return RedirectToAction("Index", "Home");

        var collaborations = await db.Collaborations
            .Where(c => c.Campaign.BrandProfileId == brand.Id)
            .Include(c => c.Campaign)
            .Include(c => c.InfluencerProfile)
            .Include(c => c.Milestones)
            .OrderByDescending(c => c.Id)
            .ToListAsync();

        var options = collaborations
            .Select(c => new BrandDisputeCollabOption(
                c.Id, c.Campaign.Title, c.InfluencerProfile.FullName,
                c.Milestones.OrderBy(m => m.CreatedAt).Select(m => (m.Id, m.Title)).ToList()))
            .ToList();

        var myDisputes = await db.Disputes
            .Where(d => d.BrandProfileId == brand.Id)
            .Include(d => d.Collaboration).ThenInclude(c => c.Campaign)
            .Include(d => d.Milestone)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new BrandDisputeRow(
                d.Id, $"DS-{d.CreatedAt.Year}-{d.Id:D3}", d.Collaboration.Campaign.Title,
                d.Milestone != null ? d.Milestone.Title : "—", d.Reason, d.Status.ToString(),
                d.CreatedAt, d.ResolutionNotes))
            .ToListAsync();

        return View(new BrandDisputesViewModel { Collaborations = options, MyDisputes = myDisputes });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(int collaborationId, int? milestoneId, string reason, string statement, List<IFormFile>? evidenceFiles)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null) return RedirectToAction("Index", "Home");

        var collaboration = await db.Collaborations
            .Include(c => c.Campaign)
            .Include(c => c.InfluencerProfile)
            .FirstOrDefaultAsync(c => c.Id == collaborationId && c.Campaign.BrandProfileId == brand.Id);

        if (collaboration is null)
        {
            TempData["DisputeError"] = "Choose a valid campaign.";
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(reason) || string.IsNullOrWhiteSpace(statement))
        {
            TempData["DisputeError"] = "Enter a reason and describe what happened.";
            return RedirectToAction(nameof(Index));
        }

        var existing = await db.Disputes
            .Where(d => d.CollaborationId == collaborationId && d.MilestoneId == milestoneId && d.Status != DisputeStatus.Resolved)
            .FirstOrDefaultAsync();

        Dispute dispute;
        if (existing is not null)
        {
            dispute = existing;
        }
        else
        {
            dispute = new Dispute
            {
                CollaborationId = collaboration.Id,
                MilestoneId = milestoneId,
                BrandProfileId = brand.Id,
                InfluencerProfileId = collaboration.InfluencerProfileId,
                Reason = reason.Trim(),
                RaisedBy = ProposalInitiator.Brand
            };
            db.Disputes.Add(dispute);
        }

        dispute.BrandStatement = statement.Trim();
        dispute.BrandStatementAt = DateTime.UtcNow;
        if (dispute.Status == DisputeStatus.Open && existing is not null) dispute.Status = DisputeStatus.UnderReview;

        if (evidenceFiles is not null)
        {
            foreach (var file in evidenceFiles.Where(f => f.Length > 0).Take(5))
            {
                var (url, _, error) = await mediaUploads.SaveMediaAsync(file, "disputes/evidence");
                if (url is not null)
                {
                    dispute.Evidence.Add(new DisputeEvidence
                    {
                        UploadedBy = ProposalInitiator.Brand,
                        FileName = file.FileName,
                        FileUrl = url,
                        FileSizeBytes = file.Length
                    });
                }
            }
        }

        await db.SaveChangesAsync();

        var influencerUserId = collaboration.InfluencerProfile.UserId;
        await notifications.NotifyAsync(influencerUserId, "Dispute", "A dispute was raised",
            $"{brand.CompanyName} raised a dispute on \"{collaboration.Campaign.Title}\": {reason.Trim()}", "/Notifications");

        TempData["DisputeSubmitted"] = "1";
        return RedirectToAction(nameof(Index));
    }
}
