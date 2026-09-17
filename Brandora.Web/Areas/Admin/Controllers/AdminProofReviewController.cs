using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Brandora.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Areas.Admin.Controllers;

public record UpcomingDeadlineRow(string BrandName, string MilestoneTitle, DateTime DueDate, int MilestoneId);
public record TopBrandRow(string BrandName, int Count);

public class AdminProofReviewViewModel
{
    public List<Milestone> Milestones { get; set; } = new();
    public int TotalCount { get; set; }
    public int SubmittedCount { get; set; }
    public int RevisionCount { get; set; }
    public int ApprovedCount { get; set; }
    public int PaidCount { get; set; }
    public List<UpcomingDeadlineRow> UpcomingDeadlines { get; set; } = new();
    public List<TopBrandRow> TopBrands { get; set; } = new();
}

public class AdminProofReviewController(ApplicationDbContext db, NotificationService notifications) : AdminControllerBase(db)
{
    public async Task<IActionResult> Index(MilestoneStatus? status, string? search)
    {
        await LoadAdminChromeAsync();
        ViewData["ActiveNav"] = "ProofReview";
        ViewData["Title"] = "Proof-of-Post Review";
        ViewData["HasCustomHero"] = true;
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Proof-of-Post Review", null) };
        ViewData["StatusFilter"] = status;
        ViewData["Search"] = search ?? "";

        var baseQuery = db.Milestones
            .Include(m => m.Collaboration).ThenInclude(c => c.Campaign).ThenInclude(c => c.BrandProfile)
            .Include(m => m.Collaboration).ThenInclude(c => c.InfluencerProfile)
            .Where(m => m.ProofUrl != null);

        var vm = new AdminProofReviewViewModel
        {
            TotalCount = await baseQuery.CountAsync(),
            SubmittedCount = await baseQuery.CountAsync(m => m.Status == MilestoneStatus.Submitted),
            RevisionCount = await baseQuery.CountAsync(m => m.Status == MilestoneStatus.RevisionRequested),
            ApprovedCount = await baseQuery.CountAsync(m => m.Status == MilestoneStatus.Approved),
            PaidCount = await baseQuery.CountAsync(m => m.Status == MilestoneStatus.Paid)
        };

        var query = baseQuery.AsQueryable();

        if (status is not null)
        {
            query = query.Where(m => m.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(m =>
                m.Title.Contains(search) ||
                m.Collaboration.Campaign.Title.Contains(search) ||
                m.Collaboration.Campaign.BrandProfile.CompanyName.Contains(search) ||
                m.Collaboration.InfluencerProfile.FullName.Contains(search));
        }

        vm.Milestones = await query.OrderByDescending(m => m.CreatedAt).ToListAsync();

        vm.UpcomingDeadlines = await baseQuery
            .Where(m => m.Status != MilestoneStatus.Approved && m.Status != MilestoneStatus.Paid && m.DueDate != null && m.DueDate >= DateTime.UtcNow)
            .OrderBy(m => m.DueDate)
            .Take(3)
            .Select(m => new UpcomingDeadlineRow(m.Collaboration.Campaign.BrandProfile.CompanyName, m.Title, m.DueDate!.Value, m.Id))
            .ToListAsync();

        vm.TopBrands = (await baseQuery
            .GroupBy(m => m.Collaboration.Campaign.BrandProfile.CompanyName)
            .Select(g => new TopBrandRow(g.Key, g.Count()))
            .ToListAsync())
            .OrderByDescending(b => b.Count)
            .Take(5)
            .ToList();

        return View(vm);
    }

    public async Task<IActionResult> Details(int id)
    {
        await LoadAdminChromeAsync();
        ViewData["ActiveNav"] = "ProofReview";
        ViewData["Title"] = "Review Proof of Post";
        ViewData["HasCustomHero"] = true;
        ViewData["Breadcrumb"] = new List<(string, string?)>
        {
            ("Proof-of-Post Review", "/Admin/AdminProofReview/Index"),
            ("Review", null)
        };

        var milestone = await db.Milestones
            .Include(m => m.Collaboration).ThenInclude(c => c.Campaign).ThenInclude(c => c.BrandProfile)
            .Include(m => m.Collaboration).ThenInclude(c => c.InfluencerProfile).ThenInclude(i => i.User)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (milestone is not null)
        {
            var siblingMilestones = await db.Milestones
                .Where(m => m.CollaborationId == milestone.CollaborationId)
                .OrderBy(m => m.CreatedAt)
                .Select(m => m.Id)
                .ToListAsync();

            ViewData["MilestoneIndex"] = siblingMilestones.IndexOf(milestone.Id) + 1;
            ViewData["MilestoneTotal"] = siblingMilestones.Count;
            ViewData["InfluencerMilestoneCount"] = await db.Milestones
                .CountAsync(m => m.Collaboration.InfluencerProfileId == milestone.Collaboration.InfluencerProfileId);
        }

        return View((object?)milestone);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        var milestone = await db.Milestones
            .Include(m => m.Collaboration).ThenInclude(c => c.InfluencerProfile)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (milestone is null) return NotFound();

        if (milestone.Status == MilestoneStatus.Submitted || milestone.Status == MilestoneStatus.RevisionRequested)
        {
            milestone.Status = MilestoneStatus.Approved;

            await notifications.NotifyAsync(
                milestone.Collaboration.InfluencerProfile.UserId,
                "Milestone",
                "Milestone approved",
                $"\"{milestone.Title}\" was approved by Admin and is ready for payment.",
                $"/Milestones/Detail/{milestone.Id}");

            await db.SaveChangesAsync();
        }

        return RedirectToAction("Details", new { id });
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

            await notifications.NotifyAsync(
                milestone.Collaboration.InfluencerProfile.UserId,
                "Milestone",
                "Revision requested",
                $"Admin requested a revision for \"{milestone.Title}\": {comment}",
                $"/Milestones/Detail/{milestone.Id}");

            await db.SaveChangesAsync();
        }

        return RedirectToAction("Details", new { id });
    }
}
