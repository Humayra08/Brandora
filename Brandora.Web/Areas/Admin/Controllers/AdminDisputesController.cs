using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Brandora.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Areas.Admin.Controllers;

public class AdminDisputesController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, NotificationService notifications) : AdminControllerBase(db)
{
    public async Task<IActionResult> Index(DisputeStatus? status)
    {
        await LoadAdminChromeAsync();
        ViewData["ActiveNav"] = "Disputes";
        ViewData["Title"] = "Dispute Resolution";
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Dispute Resolution", null) };
        ViewData["StatusFilter"] = status;

        var query = db.Disputes
            .Include(d => d.BrandProfile)
            .Include(d => d.InfluencerProfile)
            .Include(d => d.Collaboration).ThenInclude(c => c.Campaign)
            .Include(d => d.Milestone)
            .AsQueryable();

        if (status is not null)
        {
            query = query.Where(d => d.Status == status);
        }

        var disputes = await query.OrderByDescending(d => d.CreatedAt).ToListAsync();
        return View(disputes);
    }

    public async Task<IActionResult> Details(int id)
    {
        await LoadAdminChromeAsync();
        ViewData["ActiveNav"] = "Disputes";
        ViewData["Title"] = "Dispute Detail";
        ViewData["Breadcrumb"] = new List<(string, string?)>
        {
            ("Dispute Resolution", "/Admin/AdminDisputes/Index"),
            ("Detail", null)
        };

        var dispute = await db.Disputes
            .Include(d => d.BrandProfile)
            .Include(d => d.InfluencerProfile)
            .Include(d => d.Collaboration).ThenInclude(c => c.Campaign)
            .Include(d => d.Milestone)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (dispute is null) return NotFound();

        var conversation = await db.Conversations
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c =>
                c.BrandProfileId == dispute.BrandProfileId &&
                c.InfluencerProfileId == dispute.InfluencerProfileId);

        ViewData["Conversation"] = conversation;

        var payment = dispute.MilestoneId is not null
            ? await db.Payments.FirstOrDefaultAsync(p => p.MilestoneId == dispute.MilestoneId)
            : null;

        ViewData["Payment"] = payment;

        return View(dispute);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(int id, string outcome, string notes)
    {
        var dispute = await db.Disputes
            .Include(d => d.InfluencerProfile)
            .Include(d => d.BrandProfile)
            .Include(d => d.Milestone)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (dispute is null) return NotFound();

        if (dispute.MilestoneId is not null)
        {
            var payment = await db.Payments
                .Include(p => p.Collaboration).ThenInclude(c => c.Campaign)
                .FirstOrDefaultAsync(p => p.MilestoneId == dispute.MilestoneId);

            if (outcome == "ReleasePayment" && payment is not null && payment.Status == PaymentStatus.Pending)
            {
                payment.Status = PaymentStatus.Completed;
                payment.PaidAt = DateTime.UtcNow;
                payment.Collaboration.Campaign.SpentAmount += payment.Amount;

                if (dispute.Milestone is not null)
                {
                    dispute.Milestone.Status = MilestoneStatus.Paid;
                }
            }
            else if (outcome == "RefundBrand" && payment is not null && payment.Status == PaymentStatus.Pending)
            {
                payment.Status = PaymentStatus.Failed;
            }
        }

        dispute.Status = DisputeStatus.Resolved;
        dispute.ResolutionNotes = notes;
        dispute.ResolvedAt = DateTime.UtcNow;

        notifications.Notify(dispute.InfluencerProfile.UserId, "Dispute", "Dispute resolved", $"Your dispute was resolved: {outcome}.", $"/Admin/AdminDisputes/Details/{dispute.Id}");
        notifications.Notify(dispute.BrandProfile.UserId, "Dispute", "Dispute resolved", $"The dispute was resolved: {outcome}.", $"/Admin/AdminDisputes/Details/{dispute.Id}");

        await db.SaveChangesAsync();

        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Suspend(int id, string party)
    {
        var dispute = await db.Disputes
            .Include(d => d.BrandProfile)
            .Include(d => d.InfluencerProfile)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (dispute is null) return NotFound();

        var userId = string.Equals(party, "Brand", StringComparison.OrdinalIgnoreCase)
            ? dispute.BrandProfile.UserId
            : dispute.InfluencerProfile.UserId;

        var user = await userManager.FindByIdAsync(userId);
        if (user is not null)
        {
            await userManager.SetLockoutEnabledAsync(user, true);
            await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        }

        return RedirectToAction("Details", new { id });
    }
}
