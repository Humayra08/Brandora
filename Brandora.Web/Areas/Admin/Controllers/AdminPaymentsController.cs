using Brandora.Web.Areas.Admin.Services;
using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Areas.Admin.Controllers;

// Payment Oversight is read-only: the admin does not hold, approve or release payments. The admin
// approves the PROOF; the brand then pays, the money lands in the influencer's wallet, and the
// influencer withdraws freely. So there are only two real states, Pending and Completed, plus
// Failed for a payment that failed or was refunded after a dispute. Fields that will come from the
// payment gateway (gateway references for withdrawals, failure reasons) are placeholders until
// that work is merged.
public class AdminPaymentsController(ApplicationDbContext db) : AdminControllerBase(db)
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

    private static string PayType(Payment p) => p.EscrowStatus == EscrowStatus.Refunded ? "Refund" : "Brand Payment";

    private static string PayStatus(Payment p) => p.EscrowStatus == EscrowStatus.Refunded
        ? "Completed"
        : p.Status switch { PaymentStatus.Completed => "Completed", PaymentStatus.Failed => "Failed", _ => "Pending" };

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
            var type = PayType(p);
            rows.Add(new TxnRow(
                "P" + p.Id, PayCode(p), p.PaidAt ?? p.CreatedAt,
                camp.BrandProfile.CompanyName, camp.BrandProfile.ProfilePictureUrl, "Brand",
                camp.Title, $"CAM-{camp.CreatedAt.Year}-{camp.Id:D3}", camp.Id,
                MilestoneText(ctx, p), type, p.Amount, PayStatus(p), MethodLabel(p.Method),
                p.MilestoneId is int mid && ctx.DisputedMilestones.Contains(mid)));
        }

        foreach (var w in ctx.Withdrawals)
        {
            rows.Add(new TxnRow(
                "W" + w.Id, WdCode(w), w.ProcessedAt ?? w.RequestedAt,
                "@" + w.InfluencerProfile.PlatformUsername.TrimStart('@'), null, "Influencer",
                "Platform Wallet", "", null, "—", "Withdrawal", w.Amount, WdStatus(w), MethodLabel(w.Method), false));
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
        var refunds = rows.Where(r => r.Type == "Refund").ToList();

        // status donut: Completed / Pending / Failed / Refunded
        var status = new List<StatusSlice>
        {
            new("Completed", rows.Count(r => r.Status == "Completed" && r.Type != "Refund"), "#10b981"),
            new("Pending", rows.Count(r => r.Status == "Pending"), "#f5b921"),
            new("Failed", rows.Count(r => r.Status == "Failed"), "#ef4d7a"),
            new("Refunded", rows.Count(r => r.Type == "Refund"), "#a56bff")
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
                var paid = g.Where(m => m.Payment is { Status: PaymentStatus.Completed } && m.Payment.EscrowStatus != EscrowStatus.Refunded).Sum(m => m.Payment!.Amount);
                var paidCount = g.Count(m => m.Payment is { Status: PaymentStatus.Completed } && m.Payment.EscrowStatus != EscrowStatus.Refunded);
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

        // recent failed / refunded
        var failedRows = new List<FailedRow>();
        foreach (var r in rows.Where(r => r.Type == "Refund" || r.Status == "Failed").Take(5))
        {
            string reason = "Not recorded";
            if (r.Key.StartsWith('P') && int.TryParse(r.Key[1..], out var pid))
            {
                var pay = ctx.Payments.First(p => p.Id == pid);
                if (pay.EscrowStatus == EscrowStatus.Refunded) reason = "Refunded after a dispute";
            }
            else if (r.Key.StartsWith('W')) reason = "Withdrawal failed";
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
            RefundTotal = refunds.Sum(r => r.Amount),
            RefundTrend = Trend(refunds.Where(ThisMonth).Sum(r => r.Amount), refunds.Where(LastMonth).Sum(r => r.Amount)),
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
        var ledger = await PlatformWalletLedger.LoadAsync(db);

        if (id[0] == 'P')
        {
            var p = ctx.Payments.FirstOrDefault(x => x.Id == n);
            if (p is null) return NotFound();

            var camp = p.Collaboration.Campaign;
            var ms = p.Milestone;
            var refunded = p.EscrowStatus == EscrowStatus.Refunded;
            var status = PayStatus(p);
            var at = p.PaidAt ?? p.CreatedAt;
            ctx.DisputeByMilestone.TryGetValue(p.MilestoneId ?? 0, out var dispute);

            var history = new List<HistoryStep>
            {
                new("Payment released by the brand", p.CreatedAt, "done", "The brand released this milestone payment after the proof was approved.")
            };
            if (refunded)
                history.Add(new("Refunded to the brand", p.PaidAt ?? p.CreatedAt, "done", "Refunded after a dispute was resolved."));
            else if (p.Status == PaymentStatus.Completed)
                history.Add(new("Payment completed", p.PaidAt ?? p.CreatedAt, "done", $"Transferred to the influencer's wallet{(p.Method is null ? "" : " via " + MethodLabel(p.Method))}."));
            else if (p.Status == PaymentStatus.Failed)
                history.Add(new("Payment failed", null, "bad", "The payment did not go through."));
            else
                history.Add(new("Waiting for confirmation", null, "pending", "The payment is pending until the brand's payment is confirmed."));

            return PartialView("_TransactionDrawer", new TxnDetailVm
            {
                Code = PayCode(p),
                Type = PayType(p),
                Status = status,
                At = at,
                Amount = p.Amount,
                Method = MethodLabel(p.Method),
                Reference = string.IsNullOrWhiteSpace(p.TransactionReference) ? null : p.TransactionReference,
                InitiatedBy = "Brand · " + camp.BrandProfile.CompanyName,
                Remarks = refunded ? "Refunded to the brand after a dispute."
                    : p.Status == PaymentStatus.Completed ? "Payment confirmed for " + (ms?.Title ?? "the milestone") + "."
                    : p.Status == PaymentStatus.Failed ? "The payment failed."
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
                BrandLabel = refunded ? "Brand (Refunded)" : "Brand",

                History = history,
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
            var gatewayRef = ledger.WithdrawalGatewayReferences.GetValueOrDefault(w.Id);
            var fee = PlatformFee.Of(w.Amount);

            var history = new List<HistoryStep>
            {
                new("Withdrawal requested", w.RequestedAt, "done", "The influencer withdrew from their wallet. No admin approval is needed.")
            };
            history.Add(status switch
            {
                "Completed" => new HistoryStep("Funds transferred", w.ProcessedAt, "done", $"Sent to the influencer's {MethodLabel(w.Method)} account."),
                "Failed" => new HistoryStep("Withdrawal failed", w.ProcessedAt, "bad", "The withdrawal did not go through."),
                _ => new HistoryStep("Waiting for the payment gateway", null, "pending", "The transfer is being sent to the influencer's account.")
            });

            return PartialView("_TransactionDrawer", new TxnDetailVm
            {
                Code = WdCode(w),
                Type = "Withdrawal",
                Status = status,
                At = w.ProcessedAt ?? w.RequestedAt,
                Amount = w.Amount,
                Method = MethodLabel(w.Method),
                Reference = gatewayRef,
                InitiatedBy = "Influencer · " + w.InfluencerProfile.FullName,
                Remarks = $"Platform fee of ৳{fee:N0} (5%) is kept; the influencer receives ৳{w.Amount - fee:N0}.",

                HasCampaign = false,
                InfluencerName = w.InfluencerProfile.FullName,
                InfluencerHandle = w.InfluencerProfile.PlatformUsername,
                InfluencerProfileId = w.InfluencerProfileId,
                InfluencerLabel = "Influencer (Sender)",
                History = history
            });
        }

        return NotFound();
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
    bool Disputed);

public record StatusSlice(string Label, int Count, string Color);
public record MethodShare(string Method, int Count);
public record CampaignPayRow(int Id, string Title, string Code, string? Media, decimal Total, decimal Paid, decimal Pending, int PaidMilestones, int Milestones);
public record TimelinePoint(string Label, decimal BrandPayments, decimal Payouts);
public record FailedRow(string Key, DateTime At, string User, string Campaign, decimal Amount, string Reason);
public record HistoryStep(string Title, DateTime? At, string State, string Note);

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
    public decimal RefundTotal { get; set; }
    public decimal? RefundTrend { get; set; }
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
}
