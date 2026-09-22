using Brandora.Web.Areas.Admin.Services;
using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Brandora.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Areas.Admin.Controllers;

// Payment Oversight, on real data (rebuilt 2026-09-22 — see the Phase 11-18 plan).
//
// Brand payments are fully automatic: the admin approves the PROOF, the brand pays via a
// real bKash/Nagad checkout, and the money lands in the influencer's wallet the moment the
// gateway confirms it. There is no refund/escrow flip here anymore — neither gateway has a
// refund endpoint, so a payment stays exactly Completed/Pending/Failed forever, and dispute
// compensation is its own, separate ledger entry (see AdminDisputesController).
//
// Influencer withdrawals are the one genuinely manual step: there is no payout/disbursement
// API on either gateway, so an admin sends the money in their own real bKash/Nagad app and
// logs the real transaction reference here via MarkWithdrawalPaid.
public class AdminPaymentsController(ApplicationDbContext db, NotificationService notifications) : AdminControllerBase(db)
{
    private static string MethodLabel(PaymentMethod? m) => m switch
    {
        PaymentMethod.Bkash => "bKash",
        PaymentMethod.Nagad => "Nagad",
        PaymentMethod.BankTransfer => "Bank Transfer",
        _ => "Not recorded"
    };

    private static string MethodLabel(PayoutMethodKind m) => m switch
    {
        PayoutMethodKind.Bkash => "bKash",
        PayoutMethodKind.Nagad => "Nagad",
        _ => "Bank Transfer"
    };

    private static string PayCode(Payment p) => $"TXN-{(p.PaidAt ?? p.CreatedAt).Year}-P{p.Id:D3}";
    private static string WdCode(WithdrawalRequest w) => $"TXN-{w.RequestedAt.Year}-W{w.Id:D3}";

    private static decimal? Trend(decimal now, decimal before) =>
        before > 0 ? Math.Round((now - before) / before * 100, 0) : null;

    private sealed record Context(
        List<Payment> Payments,
        List<WithdrawalRequest> Withdrawals,
        Dictionary<int, (int Index, int Total)> MilestoneRank,
        HashSet<int> DisputedMilestones,
        Dictionary<int, Dispute> DisputeByMilestone);

    private async Task<Context> LoadContextAsync()
    {
        var payments = await db.Payments
            .Include(p => p.Milestone)
            .Include(p => p.Collaboration).ThenInclude(c => c.Campaign).ThenInclude(c => c.BrandProfile)
            .Include(p => p.Collaboration).ThenInclude(c => c.InfluencerProfile)
            .Include(p => p.Attempts)
            .ToListAsync();

        var withdrawals = await db.WithdrawalRequests
            .Include(w => w.InfluencerProfile)
            .ToListAsync();

        var milestones = await db.Milestones
            .Select(m => new { m.Id, m.CollaborationId, m.CreatedAt })
            .ToListAsync();

        var rank = new Dictionary<int, (int, int)>();
        foreach (var g in milestones.GroupBy(m => m.CollaborationId))
        {
            var ordered = g.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).ToList();
            for (var i = 0; i < ordered.Count; i++) rank[ordered[i].Id] = (i + 1, ordered.Count);
        }

        var disputes = await db.Disputes.Where(d => d.MilestoneId != null).ToListAsync();
        var byMilestone = disputes
            .GroupBy(d => d.MilestoneId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.CreatedAt).First());
        var open = disputes.Where(d => d.Status != DisputeStatus.Resolved).Select(d => d.MilestoneId!.Value).ToHashSet();

        return new Context(payments, withdrawals, rank, open, byMilestone);
    }

    private const string PayType = "Brand Payment";

    // Payment.Status only ever moves Pending -> Completed (see PaymentSettlementService) — a
    // failed checkout attempt never flips it to Failed, since the brand can just retry. So a
    // real attempt failure only shows up one level down, on PaymentAttempt. Surface it here as
    // "Failed" for display (the brand still CAN retry; the Payment row itself stays Pending in
    // the database) so it isn't silently invisible on this page and in Recent Failed Payments.
    private static bool LatestAttemptFailed(Payment p) =>
        p.Status == PaymentStatus.Pending &&
        p.Attempts.OrderByDescending(a => a.CreatedAt).FirstOrDefault() is { Status: PaymentAttemptStatus.Failed };

    private static string PayStatus(Payment p) => p.Status switch
    {
        PaymentStatus.Completed => "Completed",
        PaymentStatus.Failed => "Failed",
        _ when LatestAttemptFailed(p) => "Failed",
        _ => "Pending"
    };

    private static string WdStatus(WithdrawalRequest w) => w.Status switch
    {
        WithdrawalStatus.Approved or WithdrawalStatus.Paid => "Completed",
        WithdrawalStatus.Rejected => "Failed",
        _ => "Pending"
    };

    private static string MilestoneText(Context ctx, Payment p) =>
        p.MilestoneId is int id && ctx.MilestoneRank.TryGetValue(id, out var r) ? $"Milestone {r.Index} ({r.Index}/{r.Total})" : "—";

    // ------------------------------------------------------------------ list

    public async Task<IActionResult> Index(string? open)
    {
        await LoadAdminChromeAsync();
        ViewData["ActiveNav"] = "Payments";
        ViewData["Title"] = "Payment Oversight";
        ViewData["HasCustomHero"] = true;
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Payment Oversight", null) };

        var ctx = await LoadContextAsync();

        var rows = new List<TxnRow>();

        foreach (var p in ctx.Payments)
        {
            var camp = p.Collaboration.Campaign;
            rows.Add(new TxnRow(
                "P" + p.Id, PayCode(p), p.PaidAt ?? p.CreatedAt,
                camp.BrandProfile.CompanyName, camp.BrandProfile.ProfilePictureUrl, "Brand",
                camp.Title, $"CAM-{camp.CreatedAt.Year}-{camp.Id:D3}", camp.Id,
                MilestoneText(ctx, p), PayType, p.Amount, PayStatus(p), MethodLabel(p.Method),
                p.MilestoneId is int mid && ctx.DisputedMilestones.Contains(mid),
                p.Status == PaymentStatus.Completed ? p.BrandFeeAmount : 0m));
        }

        foreach (var w in ctx.Withdrawals)
        {
            var completed = w.Status is WithdrawalStatus.Approved or WithdrawalStatus.Paid;
            rows.Add(new TxnRow(
                "W" + w.Id, WdCode(w), w.ProcessedAt ?? w.RequestedAt,
                "@" + w.InfluencerProfile.PlatformUsername.TrimStart('@'), null, "Influencer",
                "Platform Wallet", "", null, "—", "Withdrawal", w.Amount, WdStatus(w), MethodLabel(w.Method), false,
                completed ? w.FeeAmount : 0m));
        }

        rows = rows.OrderByDescending(r => r.At).ToList();

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastStart = monthStart.AddMonths(-1);
        bool ThisMonth(TxnRow r) => r.At >= monthStart;
        bool LastMonth(TxnRow r) => r.At >= lastStart && r.At < monthStart;

        var brandPayments = rows.Where(r => r.Type == "Brand Payment" && r.Status == "Completed").ToList();
        var payouts = rows.Where(r => r.Type == "Withdrawal" && r.Status == "Completed").ToList();
        var pending = rows.Where(r => r.Status == "Pending").ToList();
        var feeEarning = rows.Where(r => r.Fee > 0).ToList();

        // status donut: Completed / Pending / Failed — no fake "Refunded" bucket, real
        // settlement code never sets a payment to anything but these three.
        var status = new List<StatusSlice>
        {
            new("Completed", rows.Count(r => r.Status == "Completed"), "#10b981"),
            new("Pending", rows.Count(r => r.Status == "Pending"), "#f5b921"),
            new("Failed", rows.Count(r => r.Status == "Failed"), "#ef4d7a")
        };

        var methods = rows.GroupBy(r => r.Method)
            .Select(g => new MethodShare(g.Key, g.Count()))
            .OrderByDescending(m => m.Count)
            .ToList();

        // campaign payment overview
        var milestones = await db.Milestones
            .Include(m => m.Payment)
            .Include(m => m.Collaboration).ThenInclude(c => c.Campaign)
            .ToListAsync();

        var campaigns = milestones
            .GroupBy(m => m.Collaboration.Campaign)
            .Select(g =>
            {
                var total = g.Sum(m => m.Amount);
                var paid = g.Where(m => m.Payment is { Status: PaymentStatus.Completed }).Sum(m => m.Payment!.Amount);
                var paidCount = g.Count(m => m.Payment is { Status: PaymentStatus.Completed });
                return new CampaignPayRow(g.Key.Id, g.Key.Title, $"CAM-{g.Key.CreatedAt.Year}-{g.Key.Id:D3}", g.Key.MediaUrl, total, paid, Math.Max(0m, total - paid), paidCount, g.Count());
            })
            .OrderByDescending(c => c.Total)
            .Take(5)
            .ToList();

        // payout timeline: brand payments vs influencer withdrawals, last 6 months
        var timeline = Enumerable.Range(0, 6)
            .Select(i => monthStart.AddMonths(i - 5))
            .Select(m => new TimelinePoint(
                m.ToString("MMM"),
                brandPayments.Where(r => r.At.Year == m.Year && r.At.Month == m.Month).Sum(r => r.Amount),
                payouts.Where(r => r.At.Year == m.Year && r.At.Month == m.Month).Sum(r => r.Amount)))
            .ToList();

        // recent failed payments/withdrawals, with the real reason where we have one
        var failedRows = new List<FailedRow>();
        foreach (var r in rows.Where(r => r.Status == "Failed").Take(5))
        {
            string reason = "Not recorded";
            if (r.Key.StartsWith('P') && int.TryParse(r.Key[1..], out var pid))
            {
                var pay = ctx.Payments.First(p => p.Id == pid);
                var lastAttempt = pay.Attempts.OrderByDescending(a => a.CreatedAt).FirstOrDefault();
                if (lastAttempt?.FailureReason is { } fr && !string.IsNullOrWhiteSpace(fr)) reason = fr;
            }
            else if (r.Key.StartsWith('W')) reason = "Withdrawal rejected by admin";
            failedRows.Add(new FailedRow(r.Key, r.At, r.UserName, r.Campaign, r.Amount, reason));
        }

        var vm = new PaymentIndexViewModel
        {
            Rows = rows,
            Total = rows.Count,
            TotalTrend = Trend(rows.Count(ThisMonth), rows.Count(LastMonth)),
            PayoutsTotal = payouts.Sum(r => r.Amount),
            PayoutsTrend = Trend(payouts.Where(ThisMonth).Sum(r => r.Amount), payouts.Where(LastMonth).Sum(r => r.Amount)),
            BrandTotal = brandPayments.Sum(r => r.Amount),
            BrandTrend = Trend(brandPayments.Where(ThisMonth).Sum(r => r.Amount), brandPayments.Where(LastMonth).Sum(r => r.Amount)),
            PendingTotal = pending.Sum(r => r.Amount),
            PendingTrend = Trend(pending.Where(ThisMonth).Sum(r => r.Amount), pending.Where(LastMonth).Sum(r => r.Amount)),
            AdminWalletIncomeTotal = feeEarning.Sum(r => r.Fee),
            AdminWalletIncomeTrend = Trend(feeEarning.Where(ThisMonth).Sum(r => r.Fee), feeEarning.Where(LastMonth).Sum(r => r.Fee)),
            Status = status,
            Methods = methods,
            Campaigns = campaigns,
            Timeline = timeline,
            Failed = failedRows,
            CampaignNames = rows.Select(r => r.Campaign).Distinct().OrderBy(c => c).ToList(),
            OpenKey = open
        };

        return View(vm);
    }

    // The old full-page details route now just opens the drawer on the list.
    public IActionResult Details(int id) => RedirectToAction(nameof(Index), new { open = "P" + id });

    // -------------------------------------------------------------- drawer

    public async Task<IActionResult> Transaction(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length < 2 || !int.TryParse(id[1..], out var n)) return NotFound();

        var ctx = await LoadContextAsync();

        if (id[0] == 'P')
        {
            var p = ctx.Payments.FirstOrDefault(x => x.Id == n);
            if (p is null) return NotFound();

            var camp = p.Collaboration.Campaign;
            var ms = p.Milestone;
            var status = PayStatus(p);
            var at = p.PaidAt ?? p.CreatedAt;
            ctx.DisputeByMilestone.TryGetValue(p.MilestoneId ?? 0, out var dispute);

            var attempts = p.Attempts
                .OrderBy(a => a.CreatedAt)
                .Select(a => new AttemptRow(a.Gateway, a.Status.ToString(), a.CreatedAt, a.CompletedAt, a.FailureReason, a.GatewayTransactionId))
                .ToList();

            var lastAttempt = p.Attempts.OrderByDescending(a => a.CreatedAt).FirstOrDefault();
            var attemptFailed = LatestAttemptFailed(p);

            var history = new List<HistoryStep>
            {
                new("Milestone released by the brand", p.CreatedAt, "done", "The brand released this milestone and started checkout.")
            };
            if (p.Status == PaymentStatus.Completed)
                history.Add(new("Payment completed", p.PaidAt ?? p.CreatedAt, "done", $"Confirmed by {MethodLabel(p.Method)} and credited to the influencer's wallet."));
            else if (p.Status == PaymentStatus.Failed)
                history.Add(new("Payment failed", lastAttempt?.CompletedAt, "bad", lastAttempt?.FailureReason ?? "The payment did not go through."));
            else if (attemptFailed)
                history.Add(new("Last attempt failed", lastAttempt?.CompletedAt, "bad", (lastAttempt?.FailureReason ?? "The payment did not go through.") + " The brand can try again."));
            else
                history.Add(new("Waiting for confirmation", null, "pending", "Pending until the gateway confirms the brand's payment."));

            return PartialView("_TransactionDrawer", new TxnDetailVm
            {
                Code = PayCode(p),
                Type = PayType,
                Status = status,
                At = at,
                Amount = p.TotalCharged,
                Method = MethodLabel(p.Method),
                Reference = string.IsNullOrWhiteSpace(p.TransactionReference) ? null : p.TransactionReference,
                InitiatedBy = "Brand · " + camp.BrandProfile.CompanyName,
                Remarks = p.Status == PaymentStatus.Completed ? "Payment confirmed for " + (ms?.Title ?? "the milestone") + "."
                    : p.Status == PaymentStatus.Failed || attemptFailed ? "The last attempt failed — see the gateway timeline below. The brand can retry."
                    : "Waiting for the brand's payment to be confirmed.",

                HasCampaign = true,
                CampaignId = camp.Id,
                CampaignTitle = camp.Title,
                CampaignCode = $"CAM-{camp.CreatedAt.Year}-{camp.Id:D3}",
                CampaignMedia = camp.MediaUrl,
                MilestoneText = MilestoneText(ctx, p),
                MilestoneTitle = ms?.Title,
                MilestoneAmount = ms?.Amount ?? p.Amount,
                DueDate = ms?.DueDate,
                Description = ms?.Description,

                BrandName = camp.BrandProfile.CompanyName,
                BrandPicture = camp.BrandProfile.ProfilePictureUrl,
                BrandProfileId = camp.BrandProfileId,
                InfluencerName = p.Collaboration.InfluencerProfile.FullName,
                InfluencerHandle = p.Collaboration.InfluencerProfile.PlatformUsername,
                InfluencerProfileId = p.Collaboration.InfluencerProfileId,
                InfluencerLabel = "Influencer (Recipient)",
                BrandLabel = "Brand",

                History = history,
                Attempts = attempts,
                Fee = p.Status == PaymentStatus.Completed ? p.BrandFeeAmount : 0m,
                NetAmount = p.Amount,
                DisputeId = dispute?.Id,
                DisputeCode = dispute is null ? null : $"DS-{dispute.CreatedAt.Year}-{dispute.Id:D3}",
                DisputeStatus = dispute?.Status.ToString()
            });
        }

        if (id[0] == 'W')
        {
            var w = ctx.Withdrawals.FirstOrDefault(x => x.Id == n);
            if (w is null) return NotFound();

            var status = WdStatus(w);

            var history = new List<HistoryStep>
            {
                new("Withdrawal requested", w.RequestedAt, "done", "The influencer requested this from their wallet balance.")
            };
            history.Add(status switch
            {
                "Completed" => new HistoryStep("Funds sent", w.ProcessedAt, "done", $"{w.ProcessedByAdmin} sent this to the influencer's {MethodLabel(w.Method)} account and logged reference {w.TransactionReference}."),
                "Failed" => new HistoryStep("Withdrawal rejected", w.ProcessedAt, "bad", $"Rejected by {w.ProcessedByAdmin}. The balance was restored automatically."),
                _ => new HistoryStep("Waiting for an admin to send it", null, "pending", "An admin needs to manually send this in their bKash/Nagad app and log the reference — there is no automatic payout API.")
            });

            return PartialView("_TransactionDrawer", new TxnDetailVm
            {
                Code = WdCode(w),
                Type = "Withdrawal",
                Status = status,
                At = w.ProcessedAt ?? w.RequestedAt,
                Amount = w.Amount,
                Method = MethodLabel(w.Method),
                Reference = w.TransactionReference,
                InitiatedBy = "Influencer · " + w.InfluencerProfile.FullName,
                Remarks = status == "Pending"
                    ? $"Sent to {w.AccountDetail} once an admin processes it."
                    : $"Sent to {w.AccountDetail}.",

                HasCampaign = false,
                InfluencerName = w.InfluencerProfile.FullName,
                InfluencerHandle = w.InfluencerProfile.PlatformUsername,
                InfluencerProfileId = w.InfluencerProfileId,
                InfluencerLabel = "Influencer (Sender)",
                History = history,
                Fee = w.FeeAmount,
                NetAmount = w.PayoutAmount,
                IsPendingWithdrawal = w.Status == WithdrawalStatus.Pending,
                WithdrawalId = w.Id
            });
        }

        return NotFound();
    }

    // ------------------------------------------------------------ processing

    // The one genuinely manual step in the whole payment system: neither bKash nor Nagad
    // exposes a payout/disbursement endpoint, only Checkout (customer pays merchant). So an
    // admin opens their own real bKash/Nagad app, sends the money to the influencer's real
    // account, and logs the real transaction reference here.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkWithdrawalPaid(int id, string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            TempData["PaymentsError"] = "Enter the real transaction reference from the bKash/Nagad app before marking this paid.";
            return RedirectToAction(nameof(Index), new { open = "W" + id });
        }

        var w = await db.WithdrawalRequests.Include(x => x.InfluencerProfile).FirstOrDefaultAsync(x => x.Id == id);
        if (w is null) return NotFound();

        if (w.Status == WithdrawalStatus.Pending)
        {
            w.Status = WithdrawalStatus.Paid;
            w.ProcessedAt = DateTime.UtcNow;
            w.TransactionReference = reference.Trim();
            w.ProcessedByAdmin = AdminName;

            await notifications.NotifyAsync(
                w.InfluencerProfile.UserId,
                "Payment",
                "Withdrawal paid",
                $"Your withdrawal of ৳{w.PayoutAmount:N0} was sent to {w.AccountDetail} (ref: {w.TransactionReference}).",
                "/InfluencerEarnings");

            await db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index), new { open = "W" + id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectWithdrawal(int id, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["PaymentsError"] = "Add a reason so the influencer knows why this was rejected.";
            return RedirectToAction(nameof(Index), new { open = "W" + id });
        }

        var w = await db.WithdrawalRequests.Include(x => x.InfluencerProfile).FirstOrDefaultAsync(x => x.Id == id);
        if (w is null) return NotFound();

        if (w.Status == WithdrawalStatus.Pending)
        {
            w.Status = WithdrawalStatus.Rejected;
            w.ProcessedAt = DateTime.UtcNow;
            w.ProcessedByAdmin = AdminName;
            // WalletService.GetWithdrawalLedgerAsync excludes Rejected from the "withdrawn"
            // sum, so the influencer's available balance is restored automatically —
            // nothing else to undo here.

            await notifications.NotifyAsync(
                w.InfluencerProfile.UserId,
                "Payment",
                "Withdrawal rejected",
                $"Your withdrawal request of ৳{w.Amount:N0} was rejected: {reason.Trim()}. The amount is back in your available balance.",
                "/InfluencerEarnings");

            await db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index), new { open = "W" + id });
    }
}

public record TxnRow(
    string Key,
    string Code,
    DateTime At,
    string UserName,
    string? UserPicture,
    string UserKind,
    string Campaign,
    string CampaignCode,
    int? CampaignId,
    string Milestone,
    string Type,
    decimal Amount,
    string Status,
    string Method,
    bool Disputed,
    decimal Fee);

public record StatusSlice(string Label, int Count, string Color);
public record MethodShare(string Method, int Count);
public record CampaignPayRow(int Id, string Title, string Code, string? Media, decimal Total, decimal Paid, decimal Pending, int PaidMilestones, int Milestones);
public record TimelinePoint(string Label, decimal BrandPayments, decimal Payouts);
public record FailedRow(string Key, DateTime At, string User, string Campaign, decimal Amount, string Reason);
public record HistoryStep(string Title, DateTime? At, string State, string Note);
public record AttemptRow(string Gateway, string Status, DateTime CreatedAt, DateTime? CompletedAt, string? FailureReason, string? GatewayTransactionId);

public class PaymentIndexViewModel
{
    public List<TxnRow> Rows { get; set; } = new();
    public int Total { get; set; }
    public decimal? TotalTrend { get; set; }
    public decimal PayoutsTotal { get; set; }
    public decimal? PayoutsTrend { get; set; }
    public decimal BrandTotal { get; set; }
    public decimal? BrandTrend { get; set; }
    public decimal PendingTotal { get; set; }
    public decimal? PendingTrend { get; set; }
    // "5% to Admin Wallet" — real BrandFeeAmount on completed brand payments plus real
    // FeeAmount on completed withdrawals, the same two real sources AdminDashboardController
    // reads for PlatformWalletBalance.
    public decimal AdminWalletIncomeTotal { get; set; }
    public decimal? AdminWalletIncomeTrend { get; set; }
    public List<StatusSlice> Status { get; set; } = new();
    public List<MethodShare> Methods { get; set; } = new();
    public List<CampaignPayRow> Campaigns { get; set; } = new();
    public List<TimelinePoint> Timeline { get; set; } = new();
    public List<FailedRow> Failed { get; set; } = new();
    public List<string> CampaignNames { get; set; } = new();
    public string? OpenKey { get; set; }
}

public class TxnDetailVm
{
    public string Code { get; set; } = "";
    public string Type { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime At { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = "";
    public string? Reference { get; set; }
    public string InitiatedBy { get; set; } = "";
    public string Remarks { get; set; } = "";

    public bool HasCampaign { get; set; }
    public int CampaignId { get; set; }
    public string CampaignTitle { get; set; } = "";
    public string CampaignCode { get; set; } = "";
    public string? CampaignMedia { get; set; }
    public string MilestoneText { get; set; } = "";
    public string? MilestoneTitle { get; set; }
    public decimal MilestoneAmount { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Description { get; set; }

    public string BrandName { get; set; } = "";
    public string? BrandPicture { get; set; }
    public int BrandProfileId { get; set; }
    public string BrandLabel { get; set; } = "Brand";
    public string InfluencerName { get; set; } = "";
    public string InfluencerHandle { get; set; } = "";
    public int InfluencerProfileId { get; set; }
    public string InfluencerLabel { get; set; } = "Influencer";

    public List<HistoryStep> History { get; set; } = new();
    public int? DisputeId { get; set; }
    public string? DisputeCode { get; set; }
    public string? DisputeStatus { get; set; }

    // Brand payments only — the real gateway call-by-call audit trail.
    public List<AttemptRow> Attempts { get; set; } = new();

    // Fee breakdown, shown for both a brand payment ("+5% to Admin Wallet" on top) and a
    // withdrawal ("5% kept from this withdrawal").
    public decimal Fee { get; set; }
    public decimal NetAmount { get; set; }

    // Withdrawals only — lets the drawer show Mark Paid / Reject for a still-Pending one.
    public bool IsPendingWithdrawal { get; set; }
    public int WithdrawalId { get; set; }
}
