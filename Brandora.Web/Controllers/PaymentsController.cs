using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Brandora.Web.Models.Payments;
using Brandora.Web.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Controllers;

public class PaymentsController(UserManager<ApplicationUser> userManager, ApplicationDbContext db, PaymentSettlementService settlement, IConfiguration config)
    : BrandControllerBase(userManager, db)
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

        // Ongoing pipeline preview: every not-yet-paid milestone across this brand's
        // campaigns (with its Payment, if one has been released), ranked so the ones that
        // need the brand's action surface first. Counts are taken from the full list,
        // the preview itself is capped.
        var openMilestones = await db.Milestones
            .Include(m => m.Collaboration).ThenInclude(c => c.Campaign)
            .Include(m => m.Collaboration).ThenInclude(c => c.InfluencerProfile)
            .Include(m => m.Payment)
            .Where(m => m.Collaboration.Campaign.BrandProfileId == brand.Id && m.Status != MilestoneStatus.Paid)
            .ToListAsync();

        var readyToRelease = openMilestones
            .Where(m => m.Status == MilestoneStatus.Approved && m.BrandApprovedAt is not null && m.Payment is null)
            .ToList();

        var ongoingMilestones = openMilestones
            .OrderByDescending(m => m.Payment is { Status: PaymentStatus.Pending })
            .ThenByDescending(m => m.Status == MilestoneStatus.Approved && m.BrandApprovedAt is not null)
            .ThenByDescending(m => m.Status == MilestoneStatus.Submitted || (m.Status == MilestoneStatus.Approved && m.BrandApprovedAt is null))
            .ThenByDescending(m => m.CreatedAt)
            .Take(6)
            .ToList();

        var vm = new PaymentListViewModel
        {
            Payments = payments,
            OngoingMilestones = ongoingMilestones,
            OngoingTotalCount = openMilestones.Count,
            ReadyToReleaseCount = readyToRelease.Count,
            ReadyToReleaseAmount = readyToRelease.Sum(m => m.Amount),
            AwaitingReviewCount = openMilestones.Count(m =>
                (m.Status == MilestoneStatus.Submitted && m.BrandApprovedAt is null) ||
                (m.Status == MilestoneStatus.Approved && m.BrandApprovedAt is null)),
            TotalPaid = payments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount),
            TotalPending = payments.Where(p => p.Status == PaymentStatus.Pending).Sum(p => p.Amount),
            CompletedCount = payments.Count(p => p.Status == PaymentStatus.Completed),
            PendingCount = payments.Count(p => p.Status == PaymentStatus.Pending),
            TotalCampaignBudget = campaigns.Sum(c => c.Budget),
            TotalCampaignSpend = campaigns.Sum(c => c.SpentAmount),
            MethodCounts = payments
                .Where(p => p.Method.HasValue)
                .GroupBy(p => p.Method!.Value)
                .ToDictionary(g => g.Key, g => g.Count()),
            PlatformFeePercent = decimal.TryParse(config["PLATFORM_COMMISSION_PERCENT"], out var pct) ? pct : 10m
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
            .Include(m => m.Payment)
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

        // A Payment already exists for this milestone (e.g. a double-click, or a repeat
        // POST to this endpoint) — never create a second one, which would let the same
        // milestone be paid out twice via bKash.
        if (milestone.Payment is not null)
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

    // Starts a real bKash Tokenized Checkout session for a released, still-pending
    // Payment and sends the Brand's browser to bKash's own hosted payment page. Nothing
    // is marked paid here — only BkashCallback, after bKash's Execute Payment API
    // confirms the money actually moved, does that.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InitiatePayment(int paymentId)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var payment = await db.Payments
            .Include(p => p.Collaboration).ThenInclude(c => c.Campaign)
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.Collaboration.Campaign.BrandProfileId == brand.Id);

        if (payment is null)
        {
            return NotFound();
        }

        var callbackUrl = Url.Action(nameof(BkashCallback), "Payments", null, Request.Scheme)!;
        var result = await settlement.InitiatePaymentAsync(payment, callbackUrl);

        if (!result.Success || result.RedirectUrl is null)
        {
            TempData["PaymentError"] = result.Error ?? "Could not start the bKash payment. Please try again.";
            return RedirectToAction("Detail", "Milestones", new { id = payment.MilestoneId });
        }

        return Redirect(result.RedirectUrl);
    }

    // bKash redirects the Brand's browser here after the checkout page, appending its own
    // "paymentID" and "status" query parameters. AllowAnonymous because the session cookie
    // isn't guaranteed to survive that external round trip — settlement safety instead
    // comes from matching the "ref" (our own Payment id) against a PaymentAttempt we
    // created, and from bKash's own Execute Payment API confirming the money moved, not
    // from anything the browser carries back.
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> BkashCallback([FromQuery(Name = "ref")] string? refValue, string? paymentID, string? status)
    {
        if (!int.TryParse(refValue, out var paymentId))
        {
            return RedirectToAction("Index", "Home");
        }

        var result = await settlement.HandleCallbackAsync(paymentId, paymentID, status);

        var milestoneId = await db.Payments
            .Where(p => p.Id == paymentId)
            .Select(p => p.MilestoneId)
            .FirstOrDefaultAsync();

        if (!result.Success)
        {
            TempData["PaymentError"] = result.Error;
        }
        else if (!result.AlreadySettled)
        {
            TempData["PaymentSuccess"] = "Payment confirmed via bKash.";
        }

        if (milestoneId is not null)
        {
            return RedirectToAction("Detail", "Milestones", new { id = milestoneId });
        }

        return RedirectToAction("Index");
    }
}
