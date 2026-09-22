using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Areas.Admin.Services;

public record EarningItem(
    int MilestoneId,
    string MilestoneTitle,
    string? ContentType,
    decimal Amount,
    decimal BrandFee,
    DateTime PaidAt,
    int CampaignId,
    string CampaignTitle,
    int BrandId,
    string BrandName,
    string? BrandPicture,
    int InfluencerId);

// One milestone's share of a withdrawal, with its REAL slice of that withdrawal's real fee
// (WithdrawalRequest.FeeAmount), proportional to how much of the withdrawal came from it —
// not a recomputed flat 5%, since the real fee is tranche/rate-based (see WalletService).
public record AllocationLine(EarningItem Earning, decimal Portion, decimal Fee);

public class WithdrawalInfo
{
    public int Id { get; init; }
    public int InfluencerId { get; init; }
    public string InfluencerName { get; init; } = "";
    public string Handle { get; init; } = "";
    public bool Verified { get; init; }
    public PayoutMethodKind Method { get; init; }
    public string AccountDetail { get; init; } = "";
    public decimal Amount { get; init; }
    public decimal Fee { get; init; }
    public decimal Net { get; init; }
    public WithdrawalStatus RawStatus { get; init; }
    public DateTime At { get; init; }
    public DateTime? ProcessedAt { get; init; }
    public string? TransactionReference { get; init; }
    public string? ProcessedByAdmin { get; init; }

    public string Code => "WD-" + Id;

    // Withdrawals no longer need admin approval to START, but DO need an admin to manually
    // send the money and mark it Paid (see AdminPaymentsController.MarkWithdrawalPaid) since
    // there's no payout API. Pending = still waiting for that; Approved/Paid = done.
    public string Status => RawStatus switch
    {
        WithdrawalStatus.Approved or WithdrawalStatus.Paid => "Completed",
        WithdrawalStatus.Rejected => "Failed",
        _ => "Processing"
    };

    public bool IsCompleted => Status == "Completed";

    public string MethodLabel => Method switch
    {
        PayoutMethodKind.Bkash => "bKash",
        PayoutMethodKind.Nagad => "Nagad",
        _ => "Bank Transfer"
    };

    public string MaskedAccount
    {
        get
        {
            var d = (AccountDetail ?? "").Trim();
            if (d.Length == 0) return "—";
            if (Method == PayoutMethodKind.BankAccount) return d.Length > 4 ? "****" + d[^4..] : d;
            return d.Length > 7 ? d[..5] + " " + d.Substring(5, 2) + "****" : d;
        }
    }

    public List<AllocationLine> Lines { get; set; } = new();
    public decimal Unallocated { get; set; }
}

public class WalletSnapshot
{
    public List<EarningItem> Earnings { get; set; } = new();
    public List<WithdrawalInfo> Withdrawals { get; set; } = new();
    public WalletLedger Ledger { get; set; } = null!;

    // The two real 5% income sources, kept separate exactly as specified: the brand's fee
    // (known the moment a payment settles) and the influencer's fee (known only once they
    // withdraw). LegacySettlementFees is the old one-sided model's bucket — same three
    // fields AdminDashboardController.PlatformWalletBalance already reads, so both pages
    // agree on the same number.
    public decimal BrandFeesCollected { get; set; }
    public decimal WithdrawalFeesCollected { get; set; }
    public decimal LegacySettlementFees { get; set; }
    public decimal TotalIncome => BrandFeesCollected + WithdrawalFeesCollected + LegacySettlementFees;

    public IEnumerable<AllocationLine> CompletedLines =>
        Withdrawals.Where(w => w.IsCompleted).SelectMany(w => w.Lines);
}

public static class PlatformWalletData
{
    // Real balance = everything the platform has actually earned (both 5% sources, plus the
    // legacy one-sided bucket) minus everything an admin has actually sent out (cash-outs and
    // dispute compensation), both real PlatformWalletTransaction rows. Same shape as
    // AdminDashboardController.PlatformWalletBalance, extended with real outflows.
    public static decimal Balance(WalletSnapshot snap) =>
        snap.TotalIncome - snap.Ledger.CompensationPaidAllTime - snap.Ledger.CashedOutTotal;

    public static async Task<WalletSnapshot> LoadAsync(ApplicationDbContext db)
    {
        var payments = await db.Payments
            .Where(p => p.Status == PaymentStatus.Completed)
            .Include(p => p.Milestone)
            .Include(p => p.Collaboration).ThenInclude(c => c.Campaign).ThenInclude(c => c.BrandProfile)
            .ToListAsync();

        var earnings = payments
            .Select(p => new EarningItem(
                p.MilestoneId ?? 0,
                p.Milestone?.Title ?? "Payment #" + p.Id,
                p.Milestone?.ContentType,
                p.Amount,
                p.BrandFeeAmount,
                p.PaidAt ?? p.CreatedAt,
                p.Collaboration.CampaignId,
                p.Collaboration.Campaign.Title,
                p.Collaboration.Campaign.BrandProfileId,
                p.Collaboration.Campaign.BrandProfile.CompanyName,
                p.Collaboration.Campaign.BrandProfile.ProfilePictureUrl,
                p.Collaboration.InfluencerProfileId))
            .OrderBy(e => e.PaidAt)
            .ToList();

        var withdrawals = (await db.WithdrawalRequests
                .Include(w => w.InfluencerProfile)
                .OrderBy(w => w.RequestedAt)
                .ToListAsync())
            .Select(w => new WithdrawalInfo
            {
                Id = w.Id,
                InfluencerId = w.InfluencerProfileId,
                InfluencerName = w.InfluencerProfile.FullName,
                Handle = w.InfluencerProfile.PlatformUsername,
                Verified = w.InfluencerProfile.VerificationStatus == VerificationStatus.Verified,
                Method = w.Method,
                AccountDetail = w.AccountDetail,
                Amount = w.Amount,
                Fee = w.FeeAmount,
                Net = w.PayoutAmount,
                RawStatus = w.Status,
                At = w.RequestedAt,
                ProcessedAt = w.ProcessedAt,
                TransactionReference = w.TransactionReference,
                ProcessedByAdmin = w.ProcessedByAdmin
            })
            .ToList();

        Allocate(earnings, withdrawals);

        return new WalletSnapshot
        {
            Earnings = earnings,
            Withdrawals = withdrawals,
            Ledger = await PlatformWalletLedger.LoadAsync(db),
            BrandFeesCollected = payments.Sum(p => p.BrandFeeAmount),
            WithdrawalFeesCollected = withdrawals.Where(w => w.IsCompleted).Sum(w => w.Fee),
            LegacySettlementFees = payments.Sum(p => p.PlatformFeeAmount)
        };
    }

    // A withdrawal is one lump sum from the influencer's balance. To show which campaign and
    // milestone it came from, each withdrawal (failed ones excluded) is assigned to that
    // influencer's paid milestones, oldest first, and its REAL fee (WithdrawalRequest.FeeAmount)
    // is split across those lines proportionally, so the lines always add up to both the
    // amount and the real fee.
    private static void Allocate(List<EarningItem> earnings, List<WithdrawalInfo> withdrawals)
    {
        foreach (var group in withdrawals.GroupBy(w => w.InfluencerId))
        {
            var pool = earnings
                .Where(e => e.InfluencerId == group.Key)
                .OrderBy(e => e.PaidAt)
                .Select(e => (Item: e, Left: e.Amount))
                .ToList();

            foreach (var w in group.Where(w => w.Status != "Failed").OrderBy(w => w.At))
            {
                var need = w.Amount;
                for (var i = 0; i < pool.Count && need > 0; i++)
                {
                    if (pool[i].Left <= 0) continue;
                    var take = Math.Min(need, pool[i].Left);
                    var lineFee = w.Amount > 0 ? Math.Round(w.Fee * (take / w.Amount), 2) : 0m;
                    w.Lines.Add(new AllocationLine(pool[i].Item, take, lineFee));
                    pool[i] = (pool[i].Item, pool[i].Left - take);
                    need -= take;
                }

                w.Unallocated = need;
            }
        }
    }
}
