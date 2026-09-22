using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Brandora.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Controllers;

public record InfluencerDisputeCollabOption(int CollaborationId, string CampaignTitle, string BrandName, List<(int Id, string Title)> Milestones);
public record InfluencerDisputeRow(int Id, string Code, string CampaignTitle, string MilestoneTitle, string Reason, string Status, DateTime CreatedAt, string? ResolutionNotes);

public class InfluencerDisputesViewModel
{
    public InfluencerProfile Profile { get; set; } = null!;
    public List<InfluencerDisputeCollabOption> Collaborations { get; set; } = new();
    public List<InfluencerDisputeRow> MyDisputes { get; set; } = new();
    public List<Notification> Notifications { get; set; } = new();
}

// The Influencer-side "Report a Dispute" page — the real intake this whole dispute system was
// missing (Admin could only resolve disputes that got into the database some other way).
// If the Brand already reported the same milestone, this attaches the influencer's own
// statement to that SAME Dispute row (see the lookup below) instead of creating a duplicate —
// exactly what the Admin Dispute Resolution page expects (one case, both statements).
public class InfluencerDisputesController(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext db,
    MediaUploadService mediaUploads,
    NotificationService notifications) : InfluencerControllerBase(userManager, db)
{
    public async Task<IActionResult> Index()
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null) return RedirectToAction("Index", "Home");

        var collaborations = await db.Collaborations
            .Where(c => c.InfluencerProfileId == influencer.Id)
            .Include(c => c.Campaign).ThenInclude(camp => camp.BrandProfile)
            .Include(c => c.Milestones)
            .OrderByDescending(c => c.Id)
            .ToListAsync();

        var options = collaborations
            .Select(c => new InfluencerDisputeCollabOption(
                c.Id, c.Campaign.Title, c.Campaign.BrandProfile.CompanyName,
                c.Milestones.OrderBy(m => m.CreatedAt).Select(m => (m.Id, m.Title)).ToList()))
            .ToList();

        var myDisputes = await db.Disputes
            .Where(d => d.InfluencerProfileId == influencer.Id)
            .Include(d => d.Collaboration).ThenInclude(c => c.Campaign)
            .Include(d => d.Milestone)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new InfluencerDisputeRow(
                d.Id, $"DS-{d.CreatedAt.Year}-{d.Id:D3}", d.Collaboration.Campaign.Title,
                d.Milestone != null ? d.Milestone.Title : "—", d.Reason, d.Status.ToString(),
                d.CreatedAt, d.ResolutionNotes))
            .ToListAsync();

        var recentNotifications = await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == influencer.UserId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(8)
            .ToListAsync();

        return View(new InfluencerDisputesViewModel
        {
            Profile = influencer,
            Collaborations = options,
            MyDisputes = myDisputes,
            Notifications = recentNotifications
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(int collaborationId, int? milestoneId, string reason, string statement, List<IFormFile>? evidenceFiles)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null) return RedirectToAction("Index", "Home");

        var collaboration = await db.Collaborations
            .Include(c => c.Campaign)
            .FirstOrDefaultAsync(c => c.Id == collaborationId && c.InfluencerProfileId == influencer.Id);

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

        // Attach to the Brand's existing open dispute for this same milestone, if there is
        // one, so Admin sees one case with both statements — not two separate ones.
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
                BrandProfileId = collaboration.Campaign.BrandProfileId,
                InfluencerProfileId = influencer.Id,
                Reason = reason.Trim(),
                RaisedBy = ProposalInitiator.Influencer
            };
            db.Disputes.Add(dispute);
        }

        dispute.InfluencerStatement = statement.Trim();
        dispute.InfluencerStatementAt = DateTime.UtcNow;
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
                        UploadedBy = ProposalInitiator.Influencer,
                        FileName = file.FileName,
                        FileUrl = url,
                        FileSizeBytes = file.Length
                    });
                }
            }
        }

        await db.SaveChangesAsync();

        var brandUserId = await db.BrandProfiles.Where(b => b.Id == collaboration.Campaign.BrandProfileId).Select(b => b.UserId).FirstAsync();
        await notifications.NotifyAsync(brandUserId, "Dispute", "A dispute was raised",
            $"{influencer.FullName} raised a dispute on \"{collaboration.Campaign.Title}\": {reason.Trim()}", "/Notifications");

        TempData["DisputeSubmitted"] = "1";
        return RedirectToAction(nameof(Index));
    }
}
