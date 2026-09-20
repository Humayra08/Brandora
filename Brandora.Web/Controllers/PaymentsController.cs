using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Brandora.Web.Models.Payments;
using Brandora.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Controllers;

public class PaymentsController(UserManager<ApplicationUser> userManager, ApplicationDbContext db, NotificationService notifications) : BrandControllerBase(userManager, db)
{
    public async Task<IActionResult> Index()
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var payments = await db.Payments
            .Where(p => p.Collaboration.Campaign.BrandProfileId == brand.Id)
            .Include(p => p.Collaboration).ThenInclude(c => c.Campaign)
            .Include(p => p.Collaboration).ThenInclude(c => c.InfluencerProfile)
            .Include(p => p.Milestone)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var campaigns = await db.Campaigns.Where(c => c.BrandProfileId == brand.Id).ToListAsync();

        var vm = new PaymentListViewModel
        {
            Payments = payments,
            TotalPaid = payments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount),
            TotalPending = payments.Where(p => p.Status == PaymentStatus.Pending).Sum(p => p.Amount),
            CompletedCount = payments.Count(p => p.Status == PaymentStatus.Completed),
            PendingCount = payments.Count(p => p.Status == PaymentStatus.Pending),
            TotalCampaignBudget = campaigns.Sum(c => c.Budget),
            TotalCampaignSpend = campaigns.Sum(c => c.SpentAmount),
            MethodCounts = payments
                .Where(p => p.Method.HasValue)
                .GroupBy(p => p.Method!.Value)
                .ToDictionary(g => g.Key, g => g.Count())
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Release(int milestoneId)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var milestone = await db.Milestones
            .Include(m => m.Collaboration).ThenInclude(c => c.Campaign)
            .FirstOrDefaultAsync(m => m.Id == milestoneId && m.Collaboration.Campaign.BrandProfileId == brand.Id);

        if (milestone is null)
        {
            return NotFound();
        }

        // Payment-eligible only once BOTH the Brand (BrandApprovedAt, set by
        // MilestonesController.Approve) and Admin (Status == Approved, set by
        // AdminProofReviewController.Approve) have independently signed off —
        // whichever of the two approves first is not enough on its own.
        if (milestone.Status != MilestoneStatus.Approved || milestone.BrandApprovedAt is null)
        {
            return RedirectToAction("Detail", "Milestones", new { id = milestoneId });
        }

        var payment = new Payment
        {
            CollaborationId = milestone.CollaborationId,
            MilestoneId = milestone.Id,
            Amount = milestone.Amount,
            Status = PaymentStatus.Pending
        };

        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        return RedirectToAction("Detail", "Milestones", new { id = milestoneId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(int id)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var payment = await db.Payments
            .Include(p => p.Collaboration).ThenInclude(c => c.Campaign)
            .Include(p => p.Collaboration).ThenInclude(c => c.InfluencerProfile)
            .Include(p => p.Milestone)
            .FirstOrDefaultAsync(p => p.Id == id && p.Collaboration.Campaign.BrandProfileId == brand.Id);

        if (payment is null)
        {
            return NotFound();
        }

        if (payment.Status == PaymentStatus.Pending)
        {
            payment.Status = PaymentStatus.Completed;
            payment.PaidAt = DateTime.UtcNow;

            if (payment.Milestone is not null)
            {
                payment.Milestone.Status = MilestoneStatus.Paid;
            }

            payment.Collaboration.Campaign.SpentAmount += payment.Amount;

            await notifications.NotifyAsync(
                payment.Collaboration.InfluencerProfile.UserId,
                "Payment",
                "Payment received",
                $"৳{payment.Amount:N0} confirmed for {payment.Collaboration.Campaign.Title}.",
                "/InfluencerPayments");

            await db.SaveChangesAsync();
        }

        return RedirectToAction("Detail", "Milestones", new { id = payment.MilestoneId });
    }
}
