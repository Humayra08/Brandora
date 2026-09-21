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
            // What the Brand actually paid / will pay, platform fee included.
            TotalPaid = payments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.TotalCharged),
            TotalPending = payments.Where(p => p.Status == PaymentStatus.Pending).Sum(p => p.TotalCharged),
            TotalFeesPaid = payments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.BrandFeeAmount),
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

        // The Brand's share of the commission is fixed now, at today's rate, and paid on
        // top of the milestone amount at checkout.
        var feePercent = decimal.TryParse(config["PLATFORM_COMMISSION_PERCENT"], out var pct) ? pct : 10m;
        var payment = new Payment
        {
            CollaborationId = milestone.CollaborationId,
            MilestoneId = milestone.Id,
            Amount = milestone.Amount,
            BrandFeeAmount = PaymentSettlementService.BrandFeeFor(milestone.Amount, feePercent),
            CreatorFeePercent = Math.Clamp(feePercent, 0m, 100m),
            Status = PaymentStatus.Pending
        };

        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        return RedirectToAction("Detail", "Milestones", new { id = milestoneId });
    }

    // Starts a real checkout session — bKash or Nagad, whichever the Brand picked — for a
    // released, still-pending Payment and sends the Brand's browser to that provider's own
    // hosted payment page. Nothing is marked paid here — only the provider's callback,
    // after the provider's own server confirms the money actually moved, does that.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InitiatePayment(int paymentId, PaymentMethod method = PaymentMethod.Bkash)
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

        if (method is not (PaymentMethod.Bkash or PaymentMethod.Nagad))
        {
            TempData["PaymentError"] = "Please choose bKash or Nagad to pay.";
            return RedirectToAction("Detail", "Milestones", new { id = payment.MilestoneId });
        }

        // bKash: our own reference travels under "ref" — deliberately NOT "paymentId".
        // bKash appends its own "paymentID" (capital ID) to whatever callback URL we give
        // it, and query-string binding is case-insensitive, so "paymentId" would collide.
        // Nagad: appends its own query string (payment_ref_id, status, ...) to the URL
        // verbatim, so its callback URL must carry none of ours; the payment is found
        // again through the PaymentAttempt that holds Nagad's payment_ref_id.
        var callbackUrl = method == PaymentMethod.Nagad
            ? Url.Action(nameof(NagadCallback), "Payments", null, Request.Scheme)!
            : Url.Action(nameof(BkashCallback), "Payments", new { @ref = payment.Id }, Request.Scheme)!;

        var clientIp = HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString();
        var result = await settlement.InitiatePaymentAsync(payment, method, callbackUrl, clientIp);

        if (!result.Success || result.RedirectUrl is null)
        {
            var name = method == PaymentMethod.Nagad ? "Nagad" : "bKash";
            TempData["PaymentError"] = result.Error ?? $"Could not start the {name} payment. Please try again.";
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
        return await AfterCallbackAsync(paymentId, result, "bKash");
    }

    // Nagad redirects the Brand's browser here after its hosted payment page with
    // ?merchant=&order_id=&payment_ref_id=&status=&status_code=&message=... appended.
    // Same safety model as BkashCallback: the payment_ref_id must belong to an attempt we
    // created, and Nagad's own verify API — not this redirect — decides if it was paid.
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> NagadCallback(
        [FromQuery(Name = "payment_ref_id")] string? paymentRefId,
        [FromQuery(Name = "status")] string? status)
    {
        var paymentId = await settlement.FindPaymentIdForAttemptAsync("Nagad", paymentRefId);
        if (paymentId is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var result = await settlement.HandleCallbackAsync(paymentId.Value, paymentRefId, status);
        return await AfterCallbackAsync(paymentId.Value, result, "Nagad");
    }

    private async Task<IActionResult> AfterCallbackAsync(int paymentId, PaymentSettlementResult result, string gatewayName)
    {
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
            TempData["PaymentSuccess"] = $"Payment confirmed via {gatewayName}.";
        }

        if (milestoneId is not null)
        {
            return RedirectToAction("Detail", "Milestones", new { id = milestoneId });
        }

        return RedirectToAction("Index");
    }
}
