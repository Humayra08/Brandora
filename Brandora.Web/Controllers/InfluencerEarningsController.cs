using Brandora.Web.Data;
using Brandora.Web.Models.Dashboard;
using Brandora.Web.Models.Domain;
using Brandora.Web.Services.Payments;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Brandora.Web.Controllers;

public class InfluencerEarningsController : InfluencerControllerBase
{
    private readonly ApplicationDbContext db;
    private readonly WalletService wallet;
    public InfluencerEarningsController(UserManager<ApplicationUser> userManager, ApplicationDbContext db, WalletService wallet)
        : base(userManager, db)
    {
        this.db = db;
        this.wallet = wallet;
    }

    public async Task<IActionResult> Index(string? tab, int? campaignId, string? search, bool allActivity = false)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null) return RedirectToAction("Index", "Home");
        var collaborations = await db.Collaborations.AsNoTracking().AsSplitQuery()
            .Where(c => c.InfluencerProfileId == influencer.Id)
            .Include(c => c.Campaign).ThenInclude(c => c.BrandProfile)
            .Include(c => c.Milestones)
            .OrderByDescending(c => c.CreatedAt).ThenBy(c => c.Id).ToListAsync();
        var campaigns = collaborations.Select(c => c.Campaign).DistinctBy(c => c.Id).OrderBy(c => c.Title).ToList();
        if (campaignId.HasValue && !campaigns.Any(c => c.Id == campaignId.Value)) return NotFound();
        var payments = await db.Payments.AsNoTracking()
            .Where(p => p.Collaboration.InfluencerProfileId == influencer.Id)
            .Include(p => p.Collaboration).ThenInclude(c => c.Campaign).ThenInclude(c => c.BrandProfile)
            .Include(p => p.Milestone)
            .OrderByDescending(p => p.PaidAt ?? p.CreatedAt).ThenByDescending(p => p.Id).ToListAsync();
        var notifications = await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == influencer.UserId)
            .OrderByDescending(n => n.CreatedAt).ToListAsync();
        var payoutMethods = await db.PayoutMethods.AsNoTracking()
            .Where(m => m.InfluencerProfileId == influencer.Id)
            .OrderByDescending(m => m.IsPrimary).ThenBy(m => m.Id).ToListAsync();
        var completed = payments.Where(p => p.Status == PaymentStatus.Completed).ToList();
        var pending = payments.Where(p => p.Status == PaymentStatus.Pending).ToList();
        var activity = payments.Where(p => p.Status != PaymentStatus.Failed)
            .Select(p => new EarningsActivity(p.Status == PaymentStatus.Completed ? "Payment Received" : "Payment Processing",
                $"৳{p.Amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)} from {p.Collaboration.Campaign.BrandProfile.CompanyName}",
                p.PaidAt ?? p.CreatedAt, p.Status == PaymentStatus.Completed ? "received" : "pending"))
            .Concat(notifications.Where(n => n.Category is "Milestone" or "Proof" or "Collaboration")
                .Select(n => new EarningsActivity(n.Title, n.Body, n.CreatedAt, "proof")))
            .OrderByDescending(a => a.Date).ToList();
        search = search?.Trim();
        bool Matches(Campaign campaign) =>
            (!campaignId.HasValue || campaign.Id == campaignId.Value) &&
            (string.IsNullOrEmpty(search) || campaign.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                campaign.BrandProfile.CompanyName.Contains(search, StringComparison.OrdinalIgnoreCase));
        return View(new InfluencerEarningsViewModel
        {
            InfluencerName = influencer.FullName,
            Tab = tab is "transactions" or "payout-methods" ? tab : "milestones",
            CampaignId = campaignId, Search = search, AllActivity = allActivity,
            Received = completed.Sum(p => p.Amount), Pending = pending.Sum(p => p.Amount),
            ReceivedMilestones = completed.Where(p => p.MilestoneId.HasValue).Select(p => p.MilestoneId).Distinct().Count(),
            PendingMilestones = pending.Where(p => p.MilestoneId.HasValue).Select(p => p.MilestoneId).Distinct().Count(),
            ActiveCampaigns = collaborations.Where(c => c.Status == CollaborationStatus.Active).Select(c => c.CampaignId).Distinct().Count(),
            AvailableBalance = await ComputeAvailableBalanceAsync(influencer.Id, completed.Sum(p => p.Amount)),
            Campaigns = campaigns,
            Collaborations = collaborations.Where(c => Matches(c.Campaign)).ToList(),
            Payments = payments.Where(p => Matches(p.Collaboration.Campaign)).ToList(),
            Notifications = notifications.Take(5).ToList(),
            Activity = allActivity ? activity : activity.Take(4).ToList(),
            PayoutMethods = payoutMethods
        });
    }

    [HttpGet]
    public async Task<IActionResult> Withdraw()
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null) return RedirectToAction("Index", "Home");
        var notifications = await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == influencer.UserId)
            .OrderByDescending(n => n.CreatedAt).Take(5).ToListAsync();
        var payoutMethods = await db.PayoutMethods.AsNoTracking()
            .Where(m => m.InfluencerProfileId == influencer.Id)
            .OrderByDescending(m => m.IsPrimary).ThenBy(m => m.Id).ToListAsync();
        var history = await db.WithdrawalRequests.AsNoTracking()
            .Where(w => w.InfluencerProfileId == influencer.Id)
            .OrderByDescending(w => w.RequestedAt).ToListAsync();
        var ledger = await wallet.GetWithdrawalLedgerAsync(influencer.Id);
        return View(new WithdrawalViewModel
        {
            Header = new InfluencerEarningsViewModel { InfluencerName = influencer.FullName, Notifications = notifications },
            AvailableBalance = ledger.Available,
            FeePercent = wallet.CommissionPercent,
            Ledger = ledger,
            PayoutMethods = payoutMethods,
            History = history,
            Message = TempData["WithdrawMessage"] as string,
            Error = TempData["WithdrawError"] as string
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(int payoutMethodId, decimal amount)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null) return RedirectToAction("Index", "Home");

        var method = await db.PayoutMethods.FirstOrDefaultAsync(m => m.Id == payoutMethodId && m.InfluencerProfileId == influencer.Id);
        var ledger = await wallet.GetWithdrawalLedgerAsync(influencer.Id);
        var balance = ledger.Available;
        const decimal minimum = 500m;

        if (method is null)
        {
            TempData["WithdrawError"] = "Select a valid payout method.";
        }
        else if (amount < minimum)
        {
            TempData["WithdrawError"] = $"Minimum withdrawal amount is ৳{minimum:N0}.";
        }
        else if (amount > balance)
        {
            TempData["WithdrawError"] = "Withdrawal amount exceeds your available balance.";
        }
        else
        {
            // The platform's share is worked out on the server, cumulatively across all of
            // this creator's withdrawals (see WalletService.QuoteWithdrawal), never trusted
            // from the browser's preview.
            var quote = WalletService.QuoteWithdrawal(ledger, amount);
            db.WithdrawalRequests.Add(new WithdrawalRequest
            {
                InfluencerProfileId = influencer.Id,
                Amount = amount,
                FeePercent = quote.FeePercent,
                FeeAmount = quote.Fee,
                PayoutAmount = quote.Payout,
                Method = method.Kind,
                AccountDetail = method.AccountNumber
            });
            await db.SaveChangesAsync();
            TempData["WithdrawMessage"] = quote.Fee > 0
                ? $"Withdrawal request for ৳{amount:N0} submitted — you'll receive ৳{quote.Payout:#,0.##} after the ৳{quote.Fee:N0} platform fee. It's now pending review."
                : $"Withdrawal request for ৳{amount:N0} submitted. It's now pending review.";
        }

        return RedirectToAction("Withdraw");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPayoutMethod(PayoutMethodKind kind, string accountNumber)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null) return RedirectToAction("Index", "Home");

        accountNumber = accountNumber?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(accountNumber))
        {
            TempData["PayoutMethodError"] = "Enter an account number.";
            return RedirectToAction("Index", new { tab = "payout-methods" });
        }

        var hasExisting = await db.PayoutMethods.AnyAsync(m => m.InfluencerProfileId == influencer.Id);
        db.PayoutMethods.Add(new PayoutMethod
        {
            InfluencerProfileId = influencer.Id,
            Name = kind.ToString(),
            Kind = kind,
            AccountNumber = accountNumber,
            IsPrimary = !hasExisting
        });
        await db.SaveChangesAsync();

        return RedirectToAction("Index", new { tab = "payout-methods" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemovePayoutMethod(int id)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null) return RedirectToAction("Index", "Home");

        var method = await db.PayoutMethods.FirstOrDefaultAsync(m => m.Id == id && m.InfluencerProfileId == influencer.Id);
        if (method is not null)
        {
            db.PayoutMethods.Remove(method);
            await db.SaveChangesAsync();
        }

        return RedirectToAction("Index", new { tab = "payout-methods" });
    }

    // Earned (as credited) minus everything already requested for withdrawal — shared with
    // the Brand-side settlement code through WalletService so both agree on the numbers.
    private async Task<decimal> ComputeAvailableBalanceAsync(int influencerId, decimal? knownReceived = null) =>
        (await wallet.GetWithdrawalLedgerAsync(influencerId)).Available;
}