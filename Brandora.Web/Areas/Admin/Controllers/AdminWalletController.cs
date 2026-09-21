using Brandora.Web.Areas.Admin.Services;
using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Areas.Admin.Controllers;

public class AdminWalletController(ApplicationDbContext db) : AdminControllerBase(db)
{
    public async Task<IActionResult> Index(string? search, DateTime? from, DateTime? to)
    {
        await LoadAdminChromeAsync();
        ViewData["ActiveNav"] = "Wallet";
        ViewData["Title"] = "Platform Wallet";
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Wallet", null), ("Platform Wallet", null) };

        var snap = await PlatformWalletData.LoadAsync(db);
        var ledger = snap.Ledger;

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonthStart = monthStart.AddMonths(-1);

        static DateTime FeeDate(WithdrawalInfo w) => w.ProcessedAt ?? w.At;
        var completed = snap.Withdrawals.Where(w => w.IsCompleted).ToList();

        var feesAll = completed.Sum(w => w.Fee);
        var feesThis = completed.Where(w => FeeDate(w) >= monthStart).Sum(w => w.Fee);
        var feesLast = completed.Where(w => FeeDate(w) >= lastMonthStart && FeeDate(w) < monthStart).Sum(w => w.Fee);
        var countThis = completed.Count(w => FeeDate(w) >= monthStart);
        var countLast = completed.Count(w => FeeDate(w) >= lastMonthStart && FeeDate(w) < monthStart);

        var totalPaid = snap.Earnings.Sum(e => e.Amount);
        var withdrawnCompleted = completed.Sum(w => w.Amount);
        var feesExpected = PlatformFee.Of(Math.Max(0m, totalPaid - withdrawnCompleted));

        var feesByMonth = Enumerable.Range(0, 9)
            .Select(i => monthStart.AddMonths(i - 8))
            .Select(m => new MonthAmount(
                m.ToString("MMM"),
                completed.Where(w => FeeDate(w).Year == m.Year && FeeDate(w).Month == m.Month).Sum(w => w.Fee)))
            .ToList();

        var methodColors = new (PayoutMethodKind Kind, string Label, string Color)[]
        {
            (PayoutMethodKind.Bkash, "bKash", "#e2136e"),
            (PayoutMethodKind.Nagad, "Nagad", "#f36f21"),
            (PayoutMethodKind.BankAccount, "Bank", "#236eff")
        };
        var distribution = methodColors
            .Select(m => new { m.Label, m.Color, Amount = completed.Where(w => w.Method == m.Kind).Sum(w => w.Fee) })
            .Select(x => new DistributionSlice(x.Label, x.Amount, feesAll <= 0 ? 0 : (int)Math.Round(x.Amount / feesAll * 100), x.Color))
            .ToList();

        bool InRange(DateTime at) => (from is null || at >= from) && (to is null || at < to.Value.AddDays(1));

        var milestones = await db.Milestones
            .Include(m => m.Payment)
            .Include(m => m.Collaboration).ThenInclude(c => c.Campaign).ThenInclude(c => c.BrandProfile)
            .ToListAsync();

        var rangedLines = completed.Where(w => InRange(FeeDate(w))).SelectMany(w => w.Lines).ToList();

        var rows = milestones
            .GroupBy(m => m.Collaboration.Campaign)
            .Select(g =>
            {
                var campaign = g.Key;
                var paid = snap.Earnings.Where(e => e.CampaignId == campaign.Id && InRange(e.PaidAt)).Sum(e => e.Amount);
                var collected = rangedLines.Where(l => l.Earning.CampaignId == campaign.Id).Sum(l => l.Fee);
                var expected = Math.Max(0m, PlatformFee.Of(paid) - collected);
                var anyPending = g.Any(m => m.Payment is { Status: PaymentStatus.Pending });
                var allPaid = g.All(m => m.Status == MilestoneStatus.Paid);

                var (label, tone) = allPaid
                    ? ("Completed", "gray")
                    : anyPending ? ("Some Pending", "amber") : ("In Progress", "blue");

                return new WalletCampaignRow(
                    campaign.Id,
                    campaign.Title,
                    campaign.BrandProfile.CompanyName,
                    campaign.BrandProfile.ProfilePictureUrl,
                    g.Count(),
                    g.Sum(m => m.Amount),
                    paid,
                    collected,
                    expected,
                    label,
                    tone);
            })
            .Where(r => string.IsNullOrWhiteSpace(search)
                || r.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
                || r.BrandName.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.Gross)
            .ToList();

        var vm = new AdminWalletViewModel
        {
            Ledger = ledger,
            Campaigns = rows,
            Withdrawals = snap.Withdrawals.OrderByDescending(w => w.At).ToList(),
            Search = search ?? "",
            From = from,
            To = to,
            Balance = feesAll - ledger.CompensationPaidAllTime - ledger.CashedOutTotal,
            FeesAllTime = feesAll,
            FeesThisMonth = feesThis,
            FeesTrendPct = feesLast > 0 ? Math.Round((feesThis - feesLast) / feesLast * 100, 1) : null,
            WithdrawalsProcessed = completed.Count,
            ProcessedTrendPct = countLast > 0 ? Math.Round((decimal)(countThis - countLast) / countLast * 100, 0) : null,
            FeesExpected = feesExpected,
            FeesByMonth = feesByMonth,
            Distribution = distribution,
            OpenDisputes = await db.Disputes.CountAsync(d => d.Status != DisputeStatus.Resolved)
        };

        return View(vm);
    }

    // Right-hand drawer opened from "View" on a Campaign-wise Fee Income row.
    public async Task<IActionResult> CampaignFees(int id)
    {
        var campaign = await db.Campaigns
            .Include(c => c.BrandProfile)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (campaign is null) return NotFound();

        var snap = await PlatformWalletData.LoadAsync(db);

        var milestones = await db.Milestones
            .Include(m => m.Payment)
            .Include(m => m.Collaboration).ThenInclude(c => c.InfluencerProfile)
            .Where(m => m.Collaboration.CampaignId == id)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        var completedWithdrawals = snap.Withdrawals.Where(w => w.IsCompleted).ToList();
        var lines = completedWithdrawals
            .SelectMany(w => w.Lines.Select(l => (W: w, L: l)))
            .Where(x => x.L.Earning.CampaignId == id)
            .ToList();

        var collected = lines.Sum(x => x.L.Fee);
        var paid = milestones.Where(m => m.Payment is { Status: PaymentStatus.Completed }).Sum(m => m.Payment!.Amount);

        var rows = milestones.Select((m, i) =>
        {
            var isPaid = m.Payment is { Status: PaymentStatus.Completed };
            var portion = lines.Where(x => x.L.Earning.MilestoneId == m.Id).Sum(x => x.L.Portion);
            var state = !isPaid ? "Awaiting payment"
                : portion >= m.Amount ? "Collected"
                : portion > 0 ? "Partly collected"
                : "Expected";

            return new FeeMilestoneRow(i + 1, m.Title, m.ContentType, m.Collaboration.InfluencerProfile.FullName, m.Amount, PlatformFee.Of(m.Amount), state);
        }).ToList();

        var vm = new CampaignFeeDrawerViewModel
        {
            CampaignId = campaign.Id,
            Title = campaign.Title,
            Subtitle = campaign.Description,
            Status = campaign.Status,
            BrandName = campaign.BrandProfile.CompanyName,
            BrandPicture = campaign.BrandProfile.ProfilePictureUrl,
            BrandProfileId = campaign.BrandProfileId,
            Platform = campaign.Platform,
            StartDate = campaign.StartDate,
            Deadline = campaign.Deadline,
            Gross = milestones.Sum(m => m.Amount),
            Paid = paid,
            FeeCollected = collected,
            FeeExpected = Math.Max(0m, PlatformFee.Of(paid) - collected),
            Milestones = rows,
            Influencers = milestones.Select(m => m.Collaboration.InfluencerProfile).DistinctBy(i => i.Id).Select(i => i.FullName).ToList(),
            Transactions = lines
                .OrderByDescending(x => x.W.At)
                .Select(x => new FeeTxRow(x.W.ProcessedAt ?? x.W.At, "Withdrawal fee", x.W.InfluencerName, x.W.Code, x.L.Fee, $"5% of ৳{x.L.Portion:N0} from \"{x.L.Earning.MilestoneTitle}\""))
                .ToList()
        };

        return PartialView("_CampaignFeeDrawer", vm);
    }

    // Right-hand drawer opened from "View" on an Income by Withdrawal row.
    public async Task<IActionResult> WithdrawalDetails(int id)
    {
        var snap = await PlatformWalletData.LoadAsync(db);
        var w = snap.Withdrawals.FirstOrDefault(x => x.Id == id);
        if (w is null) return NotFound();

        var vm = new WithdrawalDrawerViewModel
        {
            Withdrawal = w,
            GatewayReference = snap.Ledger.WithdrawalGatewayReferences.GetValueOrDefault(id),
            Recent = snap.Earnings
                .Where(e => e.InfluencerId == w.InfluencerId)
                .OrderByDescending(e => e.PaidAt)
                .Take(3)
                .ToList()
        };

        return PartialView("_WithdrawalDrawer", vm);
    }
}

public record WalletCampaignRow(
    int CampaignId,
    string Title,
    string BrandName,
    string? BrandPicture,
    int MilestoneCount,
    decimal Gross,
    decimal Paid,
    decimal FeeCollected,
    decimal FeeExpected,
    string StatusLabel,
    string StatusTone);

public record DistributionSlice(string Label, decimal Amount, int Percent, string Color);

public class AdminWalletViewModel
{
    public WalletLedger Ledger { get; set; } = null!;
    public List<WalletCampaignRow> Campaigns { get; set; } = new();
    public List<WithdrawalInfo> Withdrawals { get; set; } = new();
    public string Search { get; set; } = "";
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public decimal Balance { get; set; }
    public decimal FeesAllTime { get; set; }
    public decimal FeesThisMonth { get; set; }
    public decimal? FeesTrendPct { get; set; }
    public int WithdrawalsProcessed { get; set; }
    public decimal? ProcessedTrendPct { get; set; }
    public decimal FeesExpected { get; set; }
    public List<MonthAmount> FeesByMonth { get; set; } = new();
    public List<DistributionSlice> Distribution { get; set; } = new();
    public int OpenDisputes { get; set; }
}

public record FeeMilestoneRow(int No, string Title, string? Type, string InfluencerName, decimal Amount, decimal Fee, string State);

public record FeeTxRow(DateTime At, string Type, string UserName, string Code, decimal Amount, string Note);

public class CampaignFeeDrawerViewModel
{
    public int CampaignId { get; set; }
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public CampaignStatus Status { get; set; }
    public string BrandName { get; set; } = "";
    public string? BrandPicture { get; set; }
    public int BrandProfileId { get; set; }
    public string? Platform { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? Deadline { get; set; }
    public decimal Gross { get; set; }
    public decimal Paid { get; set; }
    public decimal FeeCollected { get; set; }
    public decimal FeeExpected { get; set; }
    public List<FeeMilestoneRow> Milestones { get; set; } = new();
    public List<string> Influencers { get; set; } = new();
    public List<FeeTxRow> Transactions { get; set; } = new();
}

public class WithdrawalDrawerViewModel
{
    public WithdrawalInfo Withdrawal { get; set; } = null!;
    public string? GatewayReference { get; set; }
    public List<EarningItem> Recent { get; set; } = new();
}
