using Brandora.Web.Areas.Admin.Services;
using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Brandora.Web.Services.Email;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Areas.Admin.Controllers;

public class AdminWalletController(
    ApplicationDbContext db,
    IEmailSender emailSender,
    IConfiguration config) : AdminControllerBase(db)
{
    private static readonly string[] PayoutMethods = { "bKash", "Nagad", "Bank Transfer" };

    private static PayoutMethodKind MethodKind(string method) => method switch
    {
        "bKash" => PayoutMethodKind.Bkash,
        "Nagad" => PayoutMethodKind.Nagad,
        _ => PayoutMethodKind.BankAccount
    };

    private static string MaskAccount(string method, string account)
    {
        var d = (account ?? "").Trim();
        if (d.Length == 0) return "—";
        if (method == "Bank Transfer") return d.Length > 4 ? "****" + d[^4..] : d;
        return d.Length > 7 ? d[..5] + " " + d.Substring(5, 2) + "****" : d;
    }

    private static string PaymentMethodLabel(PaymentMethod? m) => m switch
    {
        PaymentMethod.Bkash => "bKash",
        PaymentMethod.Nagad => "Nagad",
        PaymentMethod.BankTransfer => "Bank Transfer",
        _ => "Not recorded"
    };

    private static string PayStatusLabel(Payment p) => p.Status switch
    {
        PaymentStatus.Completed => "Completed",
        PaymentStatus.Failed => "Failed",
        _ => "Pending"
    };

    // Opened from the "Withdraw from Platform Wallet" button on the Platform Wallet page.
    [HttpGet]
    public async Task<IActionResult> Withdraw()
    {
        await LoadAdminChromeAsync();
        ViewData["ActiveNav"] = "Wallet";
        ViewData["Title"] = "Withdraw from Platform Wallet";
        ViewData["HasCustomHero"] = true;
        ViewData["Breadcrumb"] = new List<(string, string?)>
        {
            ("Platform Wallet", "/Admin/AdminWallet/Index"),
            ("Withdraw from Platform Wallet", null)
        };

        var snap = await PlatformWalletData.LoadAsync(db);
        var ledger = snap.Ledger;
        var done = ledger.Cashouts.OrderByDescending(c => c.At).ToList();

        return View(new CashoutPageViewModel
        {
            Ledger = ledger,
            Balance = Math.Max(0m, PlatformWalletData.Balance(snap)),
            TotalCashedOut = ledger.CashedOutTotal,
            LastCashout = done.FirstOrDefault(),
            PendingCount = 0, // every row here is only ever logged AFTER the real money already moved
            Cashouts = done,
            GatewayCharge = 0m
        });
    }

    // Called by the confirmation dialog once the admin has ALREADY sent the money in their own
    // bKash/Nagad app — there is no payout API to call, so this just records the real reference
    // they got. Returns JSON so the page can show success/failed without a reload.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(decimal amount, string? method, string? account, string? reference)
    {
        var snap = await PlatformWalletData.LoadAsync(db);
        var balance = Math.Max(0m, PlatformWalletData.Balance(snap));
        var acct = (account ?? "").Trim();
        var reff = (reference ?? "").Trim();

        if (amount <= 0) return BadRequest(new { ok = false, error = "Enter an amount greater than ৳0." });
        if (amount > balance) return BadRequest(new { ok = false, error = "The amount is more than the available balance." });
        if (method is null || !PayoutMethods.Contains(method)) return BadRequest(new { ok = false, error = "Choose a payout method." });
        if (string.IsNullOrWhiteSpace(reff)) return BadRequest(new { ok = false, error = "Enter the real transaction reference from your bKash/Nagad app." });

        var isMobile = method != "Bank Transfer";
        var digits = new string(acct.Where(char.IsDigit).ToArray());
        if (isMobile && (digits.Length != 11 || !digits.StartsWith("01")))
            return BadRequest(new { ok = false, error = "Enter a valid 11-digit mobile number that starts with 01." });
        if (!isMobile && acct.Length < 6)
            return BadRequest(new { ok = false, error = "Enter a valid bank account number." });

        var finalAccount = isMobile ? digits : acct;

        db.PlatformWalletTransactions.Add(new PlatformWalletTransaction
        {
            Type = PlatformWalletTransactionType.CashOut,
            Amount = amount,
            Method = MethodKind(method),
            AccountDetail = finalAccount,
            GatewayReference = reff,
            ProcessedByAdmin = AdminName
        });
        await db.SaveChangesAsync();

        var masked = MaskAccount(method, finalAccount);

        // The admin gets an email record of every cash-out they log.
        if (!string.IsNullOrWhiteSpace(AdminEmail))
        {
            var (subject, html) = EmailTemplates.CashoutNotice(AdminName, amount, method, masked, reff, DateTime.UtcNow);
            await emailSender.SendAsync(AdminEmail, AdminName, subject, html);
        }

        return Ok(new { ok = true, reference = reff, account = masked, amount });
    }

    public async Task<IActionResult> CashoutDetails(int id)
    {
        var ledger = await PlatformWalletLedger.LoadAsync(db);
        var row = ledger.Cashouts.FirstOrDefault(c => c.Id == id);
        if (row is null) return NotFound();

        return PartialView("_CashoutDrawer", row);
    }

    public async Task<IActionResult> Index(string? search, DateTime? from, DateTime? to)
    {
        await LoadAdminChromeAsync();
        ViewData["ActiveNav"] = "Wallet";
        ViewData["Title"] = "Platform Wallet";
        ViewData["HasCustomHero"] = true;
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Platform Wallet", null) };

        var snap = await PlatformWalletData.LoadAsync(db);
        var ledger = snap.Ledger;

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonthStart = monthStart.AddMonths(-1);

        static DateTime FeeDate(WithdrawalInfo w) => w.ProcessedAt ?? w.At;
        var completed = snap.Withdrawals.Where(w => w.IsCompleted).ToList();

        // Two real income events, dated by when each was actually earned: the brand's 5% the
        // moment a payment settles, and the influencer's 5% the moment a withdrawal is marked
        // Paid. Combined here for the "5% to Admin Wallet" totals; kept separate as
        // BrandFeesCollected/WithdrawalFeesCollected wherever the split itself matters.
        var incomeEvents = snap.Earnings.Where(e => e.BrandFee > 0).Select(e => (At: e.PaidAt, Amount: e.BrandFee))
            .Concat(completed.Select(w => (At: FeeDate(w), Amount: w.Fee)))
            .ToList();

        var feesAll = snap.TotalIncome;
        var feesThis = incomeEvents.Where(x => x.At >= monthStart).Sum(x => x.Amount);
        var feesLast = incomeEvents.Where(x => x.At >= lastMonthStart && x.At < monthStart).Sum(x => x.Amount);
        var countThis = incomeEvents.Count(x => x.At >= monthStart);
        var countLast = incomeEvents.Count(x => x.At >= lastMonthStart && x.At < monthStart);

        // Commission rate right now — used only to ESTIMATE the withdrawal-side fee still to
        // come on money already earned but not yet withdrawn (the brand-side fee is never an
        // estimate; it's collected in full the moment the payment settles).
        var currentRatePercent = decimal.TryParse(config["PLATFORM_COMMISSION_PERCENT"], out var pct) ? Math.Clamp(pct, 0m, 100m) : 10m;

        var totalPaid = snap.Earnings.Sum(e => e.Amount);
        var withdrawnCompleted = completed.Sum(w => w.Amount);
        var feesExpected = Math.Round(Math.Max(0m, totalPaid - withdrawnCompleted) * currentRatePercent / 100m, 2);

        var feesByMonth = Enumerable.Range(0, 9)
            .Select(i => monthStart.AddMonths(i - 8))
            .Select(m => new MonthAmount(
                m.ToString("MMM"),
                incomeEvents.Where(x => x.At.Year == m.Year && x.At.Month == m.Month).Sum(x => x.Amount)))
            .ToList();

        var methodColors = new (PayoutMethodKind Kind, string Label, string Color)[]
        {
            (PayoutMethodKind.Bkash, "bKash", "#e2136e"),
            (PayoutMethodKind.Nagad, "Nagad", "#f36f21"),
            (PayoutMethodKind.BankAccount, "Bank", "#236eff")
        };
        // Brand-fee events don't carry a PayoutMethodKind (Payment.Method is a different enum),
        // so the method breakdown here covers the withdrawal-fee side only.
        var distribution = methodColors
            .Select(m => new { m.Label, m.Color, Amount = completed.Where(w => w.Method == m.Kind).Sum(w => w.Fee) })
            .Select(x => new DistributionSlice(x.Label, x.Amount, snap.WithdrawalFeesCollected <= 0 ? 0 : (int)Math.Round(x.Amount / snap.WithdrawalFeesCollected * 100), x.Color))
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
                var campaignEarnings = snap.Earnings.Where(e => e.CampaignId == campaign.Id).ToList();
                var paid = campaignEarnings.Where(e => InRange(e.PaidAt)).Sum(e => e.Amount);
                // Collected = the real brand fee (always collected in full at payment time) plus
                // whatever real withdrawal fee has been allocated back to this campaign so far.
                var brandFeeCollected = campaignEarnings.Where(e => InRange(e.PaidAt)).Sum(e => e.BrandFee);
                var withdrawalFeeCollected = rangedLines.Where(l => l.Earning.CampaignId == campaign.Id).Sum(l => l.Fee);
                var collected = brandFeeCollected + withdrawalFeeCollected;
                var expected = Math.Round(Math.Max(0m, paid - withdrawalFeeCollected) * currentRatePercent / 100m, 2);
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

        // Income by Brand: one row per real Payment, the real 5% (BrandFeeAmount) it earned
        // the platform once completed — the same real field Payment Oversight shows, just
        // filtered/listed here for wallet-income auditing specifically.
        var brandPayments = await db.Payments
            .Include(p => p.Milestone)
            .Include(p => p.Collaboration).ThenInclude(c => c.Campaign).ThenInclude(c => c.BrandProfile)
            .OrderByDescending(p => p.PaidAt ?? p.CreatedAt)
            .ToListAsync();

        var brandIncomeRows = brandPayments.Select(p => new BrandIncomeRow(
            p.Id,
            $"TXN-{(p.PaidAt ?? p.CreatedAt).Year}-P{p.Id:D3}",
            p.PaidAt ?? p.CreatedAt,
            p.Collaboration.Campaign.BrandProfile.CompanyName,
            p.Collaboration.Campaign.BrandProfile.ProfilePictureUrl,
            p.Collaboration.Campaign.Title,
            p.Collaboration.CampaignId,
            p.Milestone?.Title ?? "—",
            p.Amount,
            p.Status == PaymentStatus.Completed ? p.BrandFeeAmount : 0m,
            PaymentMethodLabel(p.Method),
            PayStatusLabel(p),
            p.TransactionReference
        )).ToList();

        var vm = new AdminWalletViewModel
        {
            Ledger = ledger,
            Campaigns = rows,
            BrandIncome = brandIncomeRows,
            Withdrawals = snap.Withdrawals.OrderByDescending(w => w.At).ToList(),
            Search = search ?? "",
            From = from,
            To = to,
            Balance = PlatformWalletData.Balance(snap),
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

        var withdrawalFeeCollected = lines.Sum(x => x.L.Fee);
        var paid = milestones.Where(m => m.Payment is { Status: PaymentStatus.Completed }).Sum(m => m.Payment!.Amount);
        var brandFeeCollected = milestones.Where(m => m.Payment is { Status: PaymentStatus.Completed }).Sum(m => m.Payment!.BrandFeeAmount);
        var collected = brandFeeCollected + withdrawalFeeCollected;

        var currentRatePercent = decimal.TryParse(config["PLATFORM_COMMISSION_PERCENT"], out var pct) ? Math.Clamp(pct, 0m, 100m) : 10m;

        var rows = milestones.Select((m, i) =>
        {
            var isPaid = m.Payment is { Status: PaymentStatus.Completed };
            var portion = lines.Where(x => x.L.Earning.MilestoneId == m.Id).Sum(x => x.L.Portion);
            var state = !isPaid ? "Awaiting payment"
                : portion >= m.Amount ? "Collected"
                : portion > 0 ? "Partly collected"
                : "Expected";
            // Real brand fee once paid (collected immediately at settlement); a
            // reference-rate estimate beforehand, exactly like the campaign-level figure.
            var fee = isPaid ? m.Payment!.BrandFeeAmount : Math.Round(m.Amount * currentRatePercent / 100m, 2);

            return new FeeMilestoneRow(i + 1, m.Title, m.ContentType, m.Collaboration.InfluencerProfile.FullName, m.Amount, fee, state);
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
            FeeExpected = Math.Round(Math.Max(0m, paid - withdrawalFeeCollected) * currentRatePercent / 100m, 2),
            Milestones = rows,
            Influencers = milestones.Select(m => m.Collaboration.InfluencerProfile).DistinctBy(i => i.Id).Select(i => i.FullName).ToList(),
            InfluencerRows = milestones
                .GroupBy(m => m.Collaboration.InfluencerProfile)
                .Select(g =>
                {
                    var influencerPaid = g.Where(m => m.Payment is { Status: PaymentStatus.Completed }).Sum(m => m.Payment!.Amount);
                    var withdrawn = lines.Where(x => x.W.InfluencerId == g.Key.Id).Sum(x => x.L.Portion);
                    var influencerCollected = lines.Where(x => x.W.InfluencerId == g.Key.Id).Sum(x => x.L.Fee);
                    return new InfluencerFeeRow(
                        g.Key.Id,
                        g.Key.FullName,
                        g.Count(),
                        influencerPaid,
                        withdrawn,
                        influencerCollected,
                        Math.Round(Math.Max(0m, influencerPaid - withdrawn) * currentRatePercent / 100m, 2));
                })
                .OrderByDescending(r => r.Paid)
                .ToList(),
            Transactions = lines
                .OrderByDescending(x => x.W.At)
                .Select(x => new FeeTxRow(x.W.ProcessedAt ?? x.W.At, "Withdrawal fee", x.W.InfluencerName, x.W.Code, x.L.Fee, $"From ৳{x.L.Portion:N0} withdrawn out of \"{x.L.Earning.MilestoneTitle}\""))
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

    // Right-hand drawer opened from "View" on an Income by Brand row.
    public async Task<IActionResult> BrandIncomeDetails(int id)
    {
        var p = await db.Payments
            .Include(p => p.Milestone)
            .Include(p => p.Collaboration).ThenInclude(c => c.Campaign).ThenInclude(c => c.BrandProfile)
            .Include(p => p.Collaboration).ThenInclude(c => c.InfluencerProfile)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (p is null) return NotFound();

        var vm = new BrandIncomeDrawerViewModel
        {
            PaymentId = p.Id,
            Code = $"TXN-{(p.PaidAt ?? p.CreatedAt).Year}-P{p.Id:D3}",
            At = p.PaidAt ?? p.CreatedAt,
            Status = PayStatusLabel(p),
            Method = PaymentMethodLabel(p.Method),
            Reference = p.TransactionReference,
            BrandName = p.Collaboration.Campaign.BrandProfile.CompanyName,
            BrandPicture = p.Collaboration.Campaign.BrandProfile.ProfilePictureUrl,
            BrandProfileId = p.Collaboration.Campaign.BrandProfileId,
            CampaignId = p.Collaboration.CampaignId,
            CampaignTitle = p.Collaboration.Campaign.Title,
            MilestoneTitle = p.Milestone?.Title ?? "—",
            InfluencerName = p.Collaboration.InfluencerProfile.FullName,
            InfluencerProfileId = p.Collaboration.InfluencerProfileId,
            MilestoneAmount = p.Amount,
            Fee = p.Status == PaymentStatus.Completed ? p.BrandFeeAmount : 0m,
            TotalCharged = p.TotalCharged
        };

        return PartialView("_BrandIncomeDrawer", vm);
    }
}

public record BrandIncomeRow(
    int PaymentId,
    string Code,
    DateTime At,
    string BrandName,
    string? BrandPicture,
    string CampaignTitle,
    int CampaignId,
    string MilestoneTitle,
    decimal Amount,
    decimal Fee,
    string Method,
    string Status,
    string? Reference);

public class BrandIncomeDrawerViewModel
{
    public int PaymentId { get; set; }
    public string Code { get; set; } = "";
    public DateTime At { get; set; }
    public string Status { get; set; } = "";
    public string Method { get; set; } = "";
    public string? Reference { get; set; }
    public string BrandName { get; set; } = "";
    public string? BrandPicture { get; set; }
    public int BrandProfileId { get; set; }
    public int CampaignId { get; set; }
    public string CampaignTitle { get; set; } = "";
    public string MilestoneTitle { get; set; } = "";
    public string InfluencerName { get; set; } = "";
    public int InfluencerProfileId { get; set; }
    public decimal MilestoneAmount { get; set; }
    public decimal Fee { get; set; }
    public decimal TotalCharged { get; set; }
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

public class CashoutPageViewModel
{
    public WalletLedger Ledger { get; set; } = null!;
    public decimal Balance { get; set; }
    public decimal TotalCashedOut { get; set; }
    public CashoutRow? LastCashout { get; set; }
    public int PendingCount { get; set; }
    public List<CashoutRow> Cashouts { get; set; } = new();
    public decimal GatewayCharge { get; set; }
}

public record DistributionSlice(string Label, decimal Amount, int Percent, string Color);

public class AdminWalletViewModel
{
    public WalletLedger Ledger { get; set; } = null!;
    public List<WalletCampaignRow> Campaigns { get; set; } = new();
    public List<BrandIncomeRow> BrandIncome { get; set; } = new();
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

public record InfluencerFeeRow(int InfluencerId, string Name, int Milestones, decimal Paid, decimal Withdrawn, decimal Collected, decimal Expected);

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
    public List<InfluencerFeeRow> InfluencerRows { get; set; } = new();
    public List<FeeTxRow> Transactions { get; set; } = new();
}

public class WithdrawalDrawerViewModel
{
    public WithdrawalInfo Withdrawal { get; set; } = null!;
    public string? GatewayReference { get; set; }
    public List<EarningItem> Recent { get; set; } = new();
}
