using Brandora.Web.Data;

namespace Brandora.Web.Areas.Admin.Services;

public static class PlatformFee
{
    public const decimal Rate = 0.05m;

    public static decimal Of(decimal amount) => Math.Round(amount * Rate, 2);
}

public record MonthAmount(string Label, decimal Amount);

public record CashoutRow(
    int Id,
    DateTime At,
    string Method,
    string Account,
    decimal Amount,
    string? GatewayReference,
    string Status,
    string? Note,
    DateTime? CompletedAt = null,
    string? AccountLabel = null)
{
    public string Code => "CO-" + Id;
}

public record SavedPayoutAccount(int Id, string Method, string Account, string Label);

public record WalletLedger(
    bool IsLive,
    decimal CompensationPaidAllTime,
    decimal CompensationThisMonth,
    decimal CompensationLastMonth,
    List<MonthAmount> CompensationByMonth,
    List<CashoutRow> Cashouts,
    Dictionary<int, string> WithdrawalGatewayReferences,
    List<SavedPayoutAccount> SavedAccounts)
{
    public decimal CashedOutTotal => Cashouts.Where(c => c.Status == "Completed").Sum(c => c.Amount);
}

// PLACEHOLDER — the single place where the payment gateway work plugs in. Everything about
// influencer withdrawals and the 5% fee is already real (see PlatformWalletData). What only the
// gateway / dispute work can supply is: admin cash-outs from the platform wallet, compensation
// payouts, and the gateway reference of each influencer withdrawal. Until then they are empty,
// IsLive is false, and the page says so instead of showing made-up numbers. When that work is
// merged, replace the body of LoadAsync with real queries; nothing else needs to change.
public static class PlatformWalletLedger
{
    public static Task<WalletLedger> LoadAsync(ApplicationDbContext db)
    {
        var now = DateTime.UtcNow;
        var months = Enumerable.Range(0, 9)
            .Select(i => now.AddMonths(i - 8))
            .Select(d => new MonthAmount(d.ToString("MMM"), 0m))
            .ToList();

        return Task.FromResult(new WalletLedger(
            IsLive: false,
            CompensationPaidAllTime: 0m,
            CompensationThisMonth: 0m,
            CompensationLastMonth: 0m,
            CompensationByMonth: months,
            Cashouts: new List<CashoutRow>(),
            WithdrawalGatewayReferences: new Dictionary<int, string>(),
            SavedAccounts: new List<SavedPayoutAccount>()));
    }
}
