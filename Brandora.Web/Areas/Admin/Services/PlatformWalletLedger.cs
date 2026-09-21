using Brandora.Web.Data;

namespace Brandora.Web.Areas.Admin.Services;

public static class PlatformFee
{
    public const decimal Rate = 0.05m;

    public static decimal Of(decimal amount) => Math.Round(amount * Rate, 2);
}

public record MonthAmount(string Label, decimal Amount);

public record WalletTransactionRow(
    DateTime At,
    string Type,
    string UserName,
    string Role,
    string? DisputeRef,
    decimal Amount,
    decimal BalanceAfter,
    string Note,
    int? CampaignId = null);

public record WalletLedger(
    bool IsLive,
    decimal FeesCollectedAllTime,
    decimal FeesCollectedThisMonth,
    decimal FeesCollectedLastMonth,
    decimal CompensationPaidAllTime,
    decimal CompensationPaidLastMonth,
    List<MonthAmount> FeesByMonth,
    List<MonthAmount> CompensationByMonth,
    List<WalletTransactionRow> Transactions,
    Dictionary<int, decimal> FeesCollectedByCampaign);

// PLACEHOLDER — the single place where the payment/wallet work (withdrawal fee ledger,
// compensation payouts) plugs in. Until the wallet tables from the payment gateway work
// exist, every "collected" and "compensation paid" figure is zero and IsLive is false, so
// the Wallet page shows an "awaiting payment data" note instead of made-up numbers.
// When that work is merged, replace the body of LoadAsync with real queries; nothing else
// in the Wallet page or the campaign fee drawer needs to change.
public static class PlatformWalletLedger
{
    public static Task<WalletLedger> LoadAsync(ApplicationDbContext db)
    {
        var now = DateTime.UtcNow;
        var months = Enumerable.Range(0, 9)
            .Select(i => now.AddMonths(i - 8))
            .Select(d => new MonthAmount(d.ToString("MMM"), 0m))
            .ToList();

        var ledger = new WalletLedger(
            IsLive: false,
            FeesCollectedAllTime: 0m,
            FeesCollectedThisMonth: 0m,
            FeesCollectedLastMonth: 0m,
            CompensationPaidAllTime: 0m,
            CompensationPaidLastMonth: 0m,
            FeesByMonth: months,
            CompensationByMonth: months.Select(m => m with { }).ToList(),
            Transactions: new List<WalletTransactionRow>(),
            FeesCollectedByCampaign: new Dictionary<int, decimal>());

        return Task.FromResult(ledger);
    }
}
